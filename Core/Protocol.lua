ChromaLink = ChromaLink or {}
ChromaLink.Protocol = {}

local config = ChromaLink.Config
local frameTypes = config.frameTypes or {
  coreStatus = 1,
  playerVitals = 2,
  playerPosition = 3,
  playerCast = 4,
  playerResources = 5,
  playerCombat = 6,
  targetPosition = 7,
  followUnitStatus = 8,
  targetVitals = 9,
  targetResources = 10,
  auxUnitCast = 11,
  auraPage = 12,
  textPage = 13,
  abilityWatch = 14
}
local headerFlags = config.headerFlags or {
  multiFrameRotation = 1,
  playerPosition = 2,
  playerCast = 4,
  expandedStats = 8,
  targetPosition = 16,
  followUnitStatus = 32,
  additionalTelemetry = 64,
  textAndAuras = 128
}

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

local function FloatToFixedInt16Q2(value)
  local number = tonumber(value) or 0
  local scaled = math.floor(number * 2 + 0.5)
  if number < 0 then
    scaled = math.ceil(number * 2 - 0.5)
  end

  if scaled < -32768 then
    scaled = -32768
  end
  if scaled > 32767 then
    scaled = 32767
  end

  if scaled < 0 then
    scaled = scaled + 65536
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
  local reservedFlags = 0

  bytes[1] = string.byte("C")
  bytes[2] = string.byte("L")
  bytes[3] = (config.protocolVersion * 16) + config.profile.numericId
  bytes[4] = (frameType * 16) + schemaId
  bytes[5] = ClampByte(sequence)
  reservedFlags = reservedFlags + ClampByte(headerFlags.multiFrameRotation)
  reservedFlags = reservedFlags + ClampByte(headerFlags.playerPosition)
  reservedFlags = reservedFlags + ClampByte(headerFlags.playerCast)
  reservedFlags = reservedFlags + ClampByte(headerFlags.expandedStats)
  reservedFlags = reservedFlags + ClampByte(headerFlags.targetPosition)
  reservedFlags = reservedFlags + ClampByte(headerFlags.followUnitStatus)
  reservedFlags = reservedFlags + ClampByte(headerFlags.additionalTelemetry)
  reservedFlags = reservedFlags + ClampByte(headerFlags.textAndAuras)
  bytes[6] = ClampByte(reservedFlags)

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

function ChromaLink.Protocol.BuildPlayerCastFrame(snapshot, sequence)
  local payload = {
    ClampByte(snapshot.castFlags),
    ClampByte(snapshot.progressPctQ8),
    0,
    0,
    0,
    0,
    ClampByte(snapshot.castTargetCode)
  }
  local index

  AppendBigEndian16(payload, 3, snapshot.durationCenti)
  AppendBigEndian16(payload, 5, snapshot.remainingCenti)

  for index = 1, 5 do
    payload[7 + index] = ClampByte(snapshot.spellLabelBytes and snapshot.spellLabelBytes[index] or 32)
  end

  return BuildFrame(payload, frameTypes.playerCast, 1, sequence)
end

function ChromaLink.Protocol.BuildPlayerResourcesFrame(snapshot, sequence)
  local payload = {}
  AppendBigEndian16(payload, 1, snapshot.manaCurrent)
  AppendBigEndian16(payload, 3, snapshot.manaMax)
  AppendBigEndian16(payload, 5, snapshot.energyCurrent)
  AppendBigEndian16(payload, 7, snapshot.energyMax)
  AppendBigEndian16(payload, 9, snapshot.powerCurrent)
  AppendBigEndian16(payload, 11, snapshot.powerMax)
  return BuildFrame(payload, frameTypes.playerResources, 1, sequence)
end

function ChromaLink.Protocol.BuildPlayerCombatFrame(snapshot, sequence)
  local payload = {
    ClampByte(snapshot.combatFlags),
    ClampByte(snapshot.combo)
  }
  AppendBigEndian16(payload, 3, snapshot.chargeCurrent)
  AppendBigEndian16(payload, 5, snapshot.chargeMax)
  AppendBigEndian16(payload, 7, snapshot.planarCurrent)
  AppendBigEndian16(payload, 9, snapshot.planarMax)
  AppendBigEndian16(payload, 11, snapshot.absorb)
  return BuildFrame(payload, frameTypes.playerCombat, 1, sequence)
end

function ChromaLink.Protocol.BuildRiftMeterCombatFrame(snapshot, sequence)
  local payload = {
    ClampByte(snapshot.riftMeterFlags),
    ClampByte(snapshot.combatCount),
    0,
    0,
    ClampByte(snapshot.activeCombatPlayerCount),
    ClampByte(snapshot.activeCombatHostileCount),
    0,
    0,
    ClampByte(snapshot.overallPlayerCount),
    ClampByte(snapshot.overallHostileCount),
    ClampByte(snapshot.overallDamageK),
    ClampByte(snapshot.overallHealingK)
  }

  AppendBigEndian16(payload, 3, snapshot.activeCombatDurationDeci)
  AppendBigEndian16(payload, 7, snapshot.overallDurationDeci)
  return BuildFrame(payload, frameTypes.riftMeterCombat, 1, sequence)
end

function ChromaLink.Protocol.BuildTargetPositionFrame(snapshot, sequence)
  local payload = {}
  AppendBigEndian32(payload, 1, FloatToFixedInt32(snapshot.x))
  AppendBigEndian32(payload, 5, FloatToFixedInt32(snapshot.y))
  AppendBigEndian32(payload, 9, FloatToFixedInt32(snapshot.z))
  return BuildFrame(payload, frameTypes.targetPosition, 1, sequence)
end

function ChromaLink.Protocol.BuildTargetVitalsFrame(snapshot, sequence)
  local payload = {}
  AppendBigEndian32(payload, 1, snapshot.healthCurrent)
  AppendBigEndian32(payload, 5, snapshot.healthMax)
  AppendBigEndian16(payload, 9, snapshot.absorb)
  payload[11] = ClampByte(snapshot.targetFlags)
  payload[12] = ClampByte(snapshot.targetLevel)
  return BuildFrame(payload, frameTypes.targetVitals, 1, sequence)
end

function ChromaLink.Protocol.BuildTargetResourcesFrame(snapshot, sequence)
  local payload = {}
  AppendBigEndian16(payload, 1, snapshot.manaCurrent)
  AppendBigEndian16(payload, 3, snapshot.manaMax)
  AppendBigEndian16(payload, 5, snapshot.energyCurrent)
  AppendBigEndian16(payload, 7, snapshot.energyMax)
  AppendBigEndian16(payload, 9, snapshot.powerCurrent)
  AppendBigEndian16(payload, 11, snapshot.powerMax)
  return BuildFrame(payload, frameTypes.targetResources, 1, sequence)
end

function ChromaLink.Protocol.BuildAuxUnitCastFrame(snapshot, sequence)
  local payload = {
    ClampByte(snapshot.unitSelectorCode),
    ClampByte(snapshot.castFlags),
    ClampByte(snapshot.progressPctQ8),
    0,
    0,
    0,
    0,
    ClampByte(snapshot.castTargetCode)
  }
  local index

  AppendBigEndian16(payload, 4, snapshot.durationCenti)
  AppendBigEndian16(payload, 6, snapshot.remainingCenti)

  for index = 1, 4 do
    payload[8 + index] = ClampByte(snapshot.spellLabelBytes and snapshot.spellLabelBytes[index] or 32)
  end

  return BuildFrame(payload, frameTypes.auxUnitCast, 1, sequence)
end

function ChromaLink.Protocol.BuildAuraPageFrame(snapshot, sequence)
  local payload = {
    ClampByte(snapshot.pageKindCode),
    ClampByte(snapshot.totalAuraCount),
    0,
    0,
    ClampByte(snapshot.entry1RemainingQ4),
    ClampByte(snapshot.entry1Stack),
    ClampByte(snapshot.entry1Flags),
    0,
    0,
    ClampByte(snapshot.entry2RemainingQ4),
    ClampByte(snapshot.entry2Stack),
    ClampByte(snapshot.entry2Flags)
  }

  AppendBigEndian16(payload, 3, snapshot.entry1Id)
  AppendBigEndian16(payload, 8, snapshot.entry2Id)
  return BuildFrame(payload, frameTypes.auraPage, 1, sequence)
end

function ChromaLink.Protocol.BuildTextPageFrame(snapshot, sequence)
  local payload = {
    ClampByte(snapshot.textKindCode),
    0,
    0
  }
  local index

  AppendBigEndian16(payload, 2, snapshot.textHash16)
  for index = 1, 9 do
    payload[3 + index] = ClampByte(snapshot.textBytes and snapshot.textBytes[index] or 32)
  end

  return BuildFrame(payload, frameTypes.textPage, 1, sequence)
end

function ChromaLink.Protocol.BuildAbilityWatchFrame(snapshot, sequence)
  local payload = {
    ClampByte(snapshot.pageIndex),
    0,
    0,
    ClampByte(snapshot.entry1CooldownQ4),
    ClampByte(snapshot.entry1Flags),
    0,
    0,
    ClampByte(snapshot.entry2CooldownQ4),
    ClampByte(snapshot.entry2Flags),
    ClampByte(snapshot.shortestCooldownQ4),
    ClampByte(snapshot.readyCount),
    ClampByte(snapshot.coolingCount)
  }

  AppendBigEndian16(payload, 2, snapshot.entry1Id)
  AppendBigEndian16(payload, 6, snapshot.entry2Id)
  return BuildFrame(payload, frameTypes.abilityWatch, 1, sequence)
end

function ChromaLink.Protocol.BuildFollowUnitStatusFrame(snapshot, sequence)
  local payload = {
    ClampByte(snapshot.slot),
    ClampByte(snapshot.followFlags),
    0,
    0,
    0,
    0,
    0,
    0,
    ClampByte(snapshot.healthPctQ8),
    ClampByte(snapshot.resourcePctQ8),
    ClampByte(snapshot.level),
    ClampByte(snapshot.callingRolePacked)
  }

  AppendBigEndian16(payload, 3, FloatToFixedInt16Q2(snapshot.xQ2))
  AppendBigEndian16(payload, 5, FloatToFixedInt16Q2(snapshot.yQ2))
  AppendBigEndian16(payload, 7, FloatToFixedInt16Q2(snapshot.zQ2))
  return BuildFrame(payload, frameTypes.followUnitStatus, 1, sequence)
end
