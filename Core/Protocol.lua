ChromaLink = ChromaLink or {}
ChromaLink.Protocol = {}

local config = ChromaLink.Config
local frameTypes = config.frameTypes or { coreStatus = 1, playerVitals = 2 }

local function ClampByte(value)
  local number = tonumber(value) or 0
  if number < 0 then
    return 0
  end
  if number > 255 then
    return 255
  end
  return math.floor(number + 0.5)
end

local function Band(left, right)
  local result = 0
  local bitValue = 1
  local a = math.floor(left or 0)
  local b = math.floor(right or 0)

  while a > 0 or b > 0 do
    local abit = math.fmod(a, 2)
    local bbit = math.fmod(b, 2)
    if abit == 1 and bbit == 1 then
      result = result + bitValue
    end
    a = math.floor(a / 2)
    b = math.floor(b / 2)
    bitValue = bitValue * 2
  end

  return result
end

local function Bxor(left, right)
  local result = 0
  local bitValue = 1
  local a = math.floor(left or 0)
  local b = math.floor(right or 0)

  while a > 0 or b > 0 do
    local abit = math.fmod(a, 2)
    local bbit = math.fmod(b, 2)
    if abit ~= bbit then
      result = result + bitValue
    end
    a = math.floor(a / 2)
    b = math.floor(b / 2)
    bitValue = bitValue * 2
  end

  return result
end

local function Lshift(value, bits)
  return math.fmod((math.floor(value or 0) * (2 ^ bits)), 4294967296)
end

local function Rshift(value, bits)
  return math.floor((math.floor(value or 0)) / (2 ^ bits))
end

local function ComputeCrc16(bytes, firstIndex, lastIndex)
  local crc = 65535
  local index
  local loop

  for index = firstIndex, lastIndex do
    crc = Bxor(crc, Lshift(bytes[index], 8))
    for loop = 1, 8 do
      if Band(crc, 32768) ~= 0 then
        crc = Band(Bxor(Lshift(crc, 1), 4129), 65535)
      else
        crc = Band(Lshift(crc, 1), 65535)
      end
    end
  end

  return crc
end

local function ComputeCrc32C(bytes)
  local crc = 4294967295
  local index
  local loop

  for index = 1, #bytes do
    crc = Bxor(crc, bytes[index])
    for loop = 1, 8 do
      if Band(crc, 1) ~= 0 then
        crc = Bxor(Rshift(crc, 1), 2197175160)
      else
        crc = Rshift(crc, 1)
      end
    end
  end

  return Bxor(crc, 4294967295)
end

local function AppendBigEndian16(bytes, offset, value)
  local clamped = math.max(0, math.min(65535, math.floor((tonumber(value) or 0) + 0.5)))
  bytes[offset] = math.floor(clamped / 256)
  bytes[offset + 1] = math.fmod(clamped, 256)
end

local function AppendBigEndian32(bytes, offset, value)
  local clamped = math.max(0, math.min(4294967295, math.floor((tonumber(value) or 0) + 0.5)))
  bytes[offset] = math.floor(clamped / 16777216)
  bytes[offset + 1] = math.floor(math.fmod(clamped, 16777216) / 65536)
  bytes[offset + 2] = math.floor(math.fmod(clamped, 65536) / 256)
  bytes[offset + 3] = math.fmod(clamped, 256)
end

local function FloatToFixedInt32(value)
  local number = tonumber(value) or 0
  local scaled = math.floor(number * 100 + 0.5)
  if scaled < 0 then
    scaled = scaled + 4294967296
  end
  return scaled
end

function ChromaLink.Protocol.EncodeBytesToSymbols(bytes)
  local symbols = {}
  local symbolIndex
  local bitIndex

  for symbolIndex = 0, 63 do
    local symbol = 0
    for bitIndex = 0, 2 do
      local streamBit = (symbolIndex * 3) + bitIndex
      local byteIndex = math.floor(streamBit / 8) + 1
      local bitInByte = 7 - math.fmod(streamBit, 8)
      local bit = Band(Rshift(bytes[byteIndex], bitInByte), 1)
      symbol = (symbol * 2) + bit
    end
    symbols[symbolIndex + 1] = symbol
  end

  return symbols
end

function ChromaLink.Protocol.ComposeSegmentSymbols(payloadSymbols)
  local symbols = {}
  local index

  for index = 1, #config.controlLeft do
    symbols[index] = config.controlLeft[index]
  end

  for index = 1, #payloadSymbols do
    symbols[8 + index] = payloadSymbols[index]
  end

  for index = 1, #config.controlRight do
    symbols[72 + index] = config.controlRight[index]
  end

  return symbols
end

local function BuildFrame(payload, frameType, schemaId, sequence)
  local bytes = {}
  local headerCrc
  local payloadCrc
  local payloadSymbols
  local index

  bytes[1] = string.byte("C")
  bytes[2] = string.byte("L")
  bytes[3] = (config.protocolVersion * 16) + config.profile.numericId
  bytes[4] = (frameType * 16) + schemaId
  bytes[5] = ClampByte(sequence)
  bytes[6] = 0

  headerCrc = ComputeCrc16(bytes, 1, 6)
  AppendBigEndian16(bytes, 7, headerCrc)

  for index = 1, 12 do
    bytes[8 + index] = payload[index]
  end

  payloadCrc = ComputeCrc32C(payload)
  AppendBigEndian32(bytes, 21, payloadCrc)
  payloadSymbols = ChromaLink.Protocol.EncodeBytesToSymbols(bytes)

  return bytes, ChromaLink.Protocol.ComposeSegmentSymbols(payloadSymbols)
end

function ChromaLink.Protocol.BuildCoreFrame(snapshot, sequence)
  local payload = {
    ClampByte(snapshot.playerStateFlags),
    ClampByte(snapshot.playerHealthPctQ8),
    ClampByte(snapshot.playerResourceKind),
    ClampByte(snapshot.playerResourcePctQ8),
    ClampByte(snapshot.targetStateFlags),
    ClampByte(snapshot.targetHealthPctQ8),
    ClampByte(snapshot.targetResourceKind),
    ClampByte(snapshot.targetResourcePctQ8),
    ClampByte(snapshot.playerLevel),
    ClampByte(snapshot.targetLevel),
    ClampByte(snapshot.playerCallingRolePacked),
    ClampByte(snapshot.targetCallingRelationPacked)
  }

  return BuildFrame(payload, frameTypes.coreStatus, 1, sequence)
end

function ChromaLink.Protocol.BuildPlayerVitalsFrame(snapshot, sequence)
  local payload = {}
  AppendBigEndian32(payload, 1, snapshot.healthCurrent)
  AppendBigEndian32(payload, 5, snapshot.healthMax)
  AppendBigEndian16(payload, 9, snapshot.resourceCurrent)
  AppendBigEndian16(payload, 11, snapshot.resourceMax)
  return BuildFrame(payload, frameTypes.playerVitals, 1, sequence)
end

function ChromaLink.Protocol.BuildPlayerPositionFrame(snapshot, sequence)
  local payload = {}
  AppendBigEndian32(payload, 1, FloatToFixedInt32(snapshot.x))
  AppendBigEndian32(payload, 5, FloatToFixedInt32(snapshot.y))
  AppendBigEndian32(payload, 9, FloatToFixedInt32(snapshot.z))
  return BuildFrame(payload, frameTypes.playerPosition, 1, sequence)
end
