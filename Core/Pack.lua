-- script name: Core/Pack.lua
-- version: 0.5.0
-- purpose: Supplies deterministic bit packing, byte writing, witness fill, and CRC helpers for ChromaLink.
-- dependencies: Core/Config.lua
-- important assumptions: Uses CRC-16/CCITT-FALSE for header validation and CRC-32C for payload validation with big-endian byte emission.
-- protocol version: ChromaLink
-- framework module role: Core packing and validation primitives
-- character count note: Character count not precomputed; measure with tooling if needed.

ChromaLink = ChromaLink or {}
ChromaLink.Pack = {}

function ChromaLink.Pack.ClampUnsigned(value, maxValue)
  local number = math.floor(tonumber(value) or 0)

  if number < 0 then
    return 0
  end

  if number > maxValue then
    return maxValue
  end

  return number
end

function ChromaLink.Pack.ClampSigned(value, minValue, maxValue)
  local number = math.floor(tonumber(value) or 0)

  if number < minValue then
    return minValue
  end

  if number > maxValue then
    return maxValue
  end

  return number
end

function ChromaLink.Pack.BitXor(leftValue, rightValue)
  local result = 0
  local bitWeight = 1
  local left = leftValue
  local right = rightValue

  while left > 0 or right > 0 do
    local leftBit = math.fmod(left, 2)
    local rightBit = math.fmod(right, 2)
    if leftBit ~= rightBit then
      result = result + bitWeight
    end
    left = math.floor(left / 2)
    right = math.floor(right / 2)
    bitWeight = bitWeight * 2
  end

  return result
end

function ChromaLink.Pack.BitAnd(leftValue, rightValue)
  local result = 0
  local bitWeight = 1
  local left = math.floor(tonumber(leftValue) or 0)
  local right = math.floor(tonumber(rightValue) or 0)

  while left > 0 and right > 0 do
    local leftBit = math.fmod(left, 2)
    local rightBit = math.fmod(right, 2)
    if leftBit == 1 and rightBit == 1 then
      result = result + bitWeight
    end
    left = math.floor(left / 2)
    right = math.floor(right / 2)
    bitWeight = bitWeight * 2
  end

  return result
end

function ChromaLink.Pack.BytesToBits(bytes)
  local bits = {}
  local byteIndex
  local bitIndex
  local cursor = 1

  for byteIndex = 1, #bytes do
    local value = bytes[byteIndex]
    for bitIndex = 7, 0, -1 do
      local bitValue = math.floor(value / (2 ^ bitIndex))
      bits[cursor] = math.fmod(bitValue, 2)
      cursor = cursor + 1
    end
  end

  return bits
end

function ChromaLink.Pack.Crc16(bytes, startIndex, endIndex)
  local crc = 0xFFFF
  local byteIndex
  local bitIndex

  for byteIndex = startIndex, endIndex do
    local value = bytes[byteIndex] or 0
    crc = ChromaLink.Pack.BitXor(crc, (value * 256))
    for bitIndex = 1, 8 do
      if crc >= 0x8000 then
        crc = ChromaLink.Pack.BitXor((crc * 2), 0x1021)
      else
        crc = crc * 2
      end
      crc = math.fmod(crc, 0x10000)
    end
  end

  return crc
end

function ChromaLink.Pack.BuildCrc32CTable()
  local tableValues = {}
  local polynomial = 0x82F63B78
  local index
  local bitIndex

  for index = 0, 255 do
    local value = index
    for bitIndex = 1, 8 do
      if math.fmod(value, 2) == 1 then
        value = ChromaLink.Pack.BitXor(polynomial, math.floor(value / 2))
      else
        value = math.floor(value / 2)
      end
    end
    tableValues[index + 1] = value
  end

  return tableValues
end

ChromaLink.Pack.Crc32CTable = ChromaLink.Pack.BuildCrc32CTable()

function ChromaLink.Pack.Crc32C(bytes, startIndex, endIndex)
  local crc = 0xFFFFFFFF
  local byteIndex

  for byteIndex = startIndex, endIndex do
    local value = bytes[byteIndex] or 0
    local lookupIndex = math.fmod(ChromaLink.Pack.BitXor(crc, value), 0x100)
    crc = ChromaLink.Pack.BitXor(ChromaLink.Pack.Crc32CTable[lookupIndex + 1], math.floor(crc / 0x100))
  end

  return ChromaLink.Pack.BitXor(crc, 0xFFFFFFFF)
end

function ChromaLink.Pack.PutUInt8(bytes, index, value)
  bytes[index] = ChromaLink.Pack.ClampUnsigned(value, 0xFF)
  return index + 1
end

function ChromaLink.Pack.PutUInt16(bytes, index, value)
  local number = ChromaLink.Pack.ClampUnsigned(value, 0xFFFF)
  bytes[index] = math.floor(number / 0x100)
  bytes[index + 1] = math.fmod(number, 0x100)
  return index + 2
end

function ChromaLink.Pack.PutUInt24(bytes, index, value)
  local number = ChromaLink.Pack.ClampUnsigned(value, 0xFFFFFF)
  bytes[index] = math.floor(number / 0x10000)
  bytes[index + 1] = math.floor(math.fmod(number, 0x10000) / 0x100)
  bytes[index + 2] = math.fmod(number, 0x100)
  return index + 3
end

function ChromaLink.Pack.PutUInt32(bytes, index, value)
  local number = ChromaLink.Pack.ClampUnsigned(value, 0xFFFFFFFF)
  bytes[index] = math.floor(number / 0x1000000)
  bytes[index + 1] = math.floor(math.fmod(number, 0x1000000) / 0x10000)
  bytes[index + 2] = math.floor(math.fmod(number, 0x10000) / 0x100)
  bytes[index + 3] = math.fmod(number, 0x100)
  return index + 4
end

function ChromaLink.Pack.PutInt24(bytes, index, value)
  local number = ChromaLink.Pack.ClampSigned(value, -0x800000, 0x7FFFFF)

  if number < 0 then
    number = 0x1000000 + number
  end

  return ChromaLink.Pack.PutUInt24(bytes, index, number)
end

function ChromaLink.Pack.FillWitness(bytes, startIndex, count)
  local index
  local value = 0xA5

  for index = startIndex, startIndex + count - 1 do
    bytes[index] = value
    if value == 0xA5 then
      value = 0x5A
    else
      value = 0xA5
    end
  end
end

-- end-of-script marker comment
