-- script name: Core/Protocol.lua
-- version: 0.5.0
-- purpose: Defines the ChromaLink hot-lane baseline transport, payload packing, and module matrix generation.
-- dependencies: Core/Config.lua, Core/Pack.lua, Core/Gather.lua
-- important assumptions: Phase 1 keeps the payload monochrome-safe, duplicates the first 3 transport header bytes on both inner edges, and prioritizes reliable lock/replay over maximum telemetry breadth.
-- protocol version: ChromaLink
-- framework module role: Core protocol definition
-- character count note: Character count not precomputed; measure with tooling if needed.

ChromaLink = ChromaLink or {}
ChromaLink.Protocol = {}

ChromaLink.Protocol.FrameTypeIds = {
  coreStatus = ChromaLink.Config.frameTypeIds.coreStatus,
  tactical = ChromaLink.Config.frameTypeIds.tactical,
  calibration = ChromaLink.Config.frameTypeIds.calibration,
  event = ChromaLink.Config.frameTypeIds.event
}

ChromaLink.Protocol.LaneIds = {
  hot = ChromaLink.Config.laneIds.hot,
  warm = ChromaLink.Config.laneIds.warm,
  cold = ChromaLink.Config.laneIds.cold,
  event = ChromaLink.Config.laneIds.event
}

ChromaLink.Protocol.FrameLayout = {
  headerBytes = ChromaLink.Config.headerBytes,
  payloadBytes = ChromaLink.Config.payloadBytes,
  payloadCrcBytes = ChromaLink.Config.payloadCrcBytes,
  duplicateHeaderBytes = ChromaLink.Config.duplicateHeaderBytes,
  totalBytes = ChromaLink.Config.transportBytes
}

function ChromaLink.Protocol.GetWitnessBytes(count)
  local bytes = {}
  ChromaLink.Pack.FillWitness(bytes, 1, count)
  return bytes
end

function ChromaLink.Protocol.BuildCorePayloadBytes(snapshot)
  local payloadBytes = {}
  local index = 1

  ChromaLink.Pack.FillWitness(payloadBytes, 1, ChromaLink.Protocol.FrameLayout.payloadBytes)

  index = ChromaLink.Pack.PutUInt16(payloadBytes, index, snapshot.stateFlags or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.playerResourceKindId or 0)
  index = ChromaLink.Pack.PutUInt24(payloadBytes, index, snapshot.playerHealthCurrent or 0)
  index = ChromaLink.Pack.PutUInt24(payloadBytes, index, snapshot.playerHealthMax or 0)
  index = ChromaLink.Pack.PutUInt24(payloadBytes, index, snapshot.playerResourceCurrent or 0)
  index = ChromaLink.Pack.PutUInt24(payloadBytes, index, snapshot.playerResourceMax or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.playerLevel or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.playerCallingCode or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.playerRoleCode or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.targetResourceKindId or 0)
  index = ChromaLink.Pack.PutUInt24(payloadBytes, index, snapshot.targetHealthCurrent or 0)
  index = ChromaLink.Pack.PutUInt24(payloadBytes, index, snapshot.targetHealthMax or 0)
  index = ChromaLink.Pack.PutUInt24(payloadBytes, index, snapshot.targetResourceCurrent or 0)
  index = ChromaLink.Pack.PutUInt24(payloadBytes, index, snapshot.targetResourceMax or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.targetLevel or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.targetFlags or 0)

  return payloadBytes, ChromaLink.Protocol.FrameLayout.payloadBytes
end

function ChromaLink.Protocol.BuildTacticalPayloadBytes(snapshot)
  local payloadBytes = {}
  local index = 1

  ChromaLink.Pack.FillWitness(payloadBytes, 1, ChromaLink.Protocol.FrameLayout.payloadBytes)

  index = ChromaLink.Pack.PutUInt16(payloadBytes, index, snapshot.tacticalMask or 0)
  index = ChromaLink.Pack.PutUInt16(payloadBytes, index, snapshot.stateFlags or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.playerCastFlags or 0)
  index = ChromaLink.Pack.PutUInt16(payloadBytes, index, snapshot.playerCastProgressQ15 or 0)
  index = ChromaLink.Pack.PutUInt16(payloadBytes, index, snapshot.playerZoneHash16 or 0)
  index = ChromaLink.Pack.PutUInt16(payloadBytes, index, snapshot.targetZoneHash16 or 0)
  index = ChromaLink.Pack.PutInt24(payloadBytes, index, snapshot.playerCoordX10 or 0)
  index = ChromaLink.Pack.PutInt24(payloadBytes, index, snapshot.playerCoordZ10 or 0)
  index = ChromaLink.Pack.PutInt24(payloadBytes, index, snapshot.targetCoordX10 or 0)
  index = ChromaLink.Pack.PutInt24(payloadBytes, index, snapshot.targetCoordZ10 or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.targetRelationCode or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.targetTierCode or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.targetTaggedCode or 0)
  index = ChromaLink.Pack.PutUInt8(payloadBytes, index, snapshot.targetCallingCode or 0)
  index = ChromaLink.Pack.PutUInt16(payloadBytes, index, snapshot.targetRadiusQ10 or 0)
  index = ChromaLink.Pack.PutUInt16(payloadBytes, index, snapshot.playerPowerAttack or 0)
  index = ChromaLink.Pack.PutUInt16(payloadBytes, index, snapshot.playerPowerSpell or 0)

  return payloadBytes, ChromaLink.Protocol.FrameLayout.payloadBytes
end

function ChromaLink.Protocol.BuildPayloadBytesForFrame(frameTypeId, snapshot)
  if frameTypeId == ChromaLink.Protocol.FrameTypeIds.coreStatus then
    return ChromaLink.Protocol.BuildCorePayloadBytes(snapshot)
  end

  if frameTypeId == ChromaLink.Protocol.FrameTypeIds.tactical then
    return ChromaLink.Protocol.BuildTacticalPayloadBytes(snapshot)
  end

  error("Unsupported ChromaLink frame type: " .. tostring(frameTypeId))
end

function ChromaLink.Protocol.BuildTransportBytes(profileId, frameTypeId, laneId, sequence, payloadBytes, payloadLength)
  local layout = ChromaLink.Protocol.FrameLayout
  local bytes = {}
  local index

  ChromaLink.Pack.FillWitness(bytes, 1, layout.totalBytes)

  bytes[1] = string.byte("C")
  bytes[2] = string.byte("L")
  bytes[3] = ((ChromaLink.Config.protocolVersion or 0) * 0x10) + math.fmod(profileId or 0, 0x10)
  bytes[4] = ((frameTypeId or 0) * 0x10) + math.fmod(laneId or 0, 0x10)
  bytes[5] = math.fmod(sequence or 0, 0x100)
  bytes[6] = math.fmod(payloadLength or 0, 0x100)

  local headerCrc = ChromaLink.Pack.Crc16(bytes, 1, 6)
  bytes[7] = math.floor(headerCrc / 0x100)
  bytes[8] = math.fmod(headerCrc, 0x100)

  for index = 1, payloadLength do
    bytes[layout.headerBytes + index] = payloadBytes[index] or 0
  end

  local payloadCrc = ChromaLink.Pack.Crc32C(payloadBytes, 1, payloadLength)
  ChromaLink.Pack.PutUInt32(bytes, layout.headerBytes + payloadLength + 1, payloadCrc)

  return bytes
end

function ChromaLink.Protocol.BuildLiveFrameBytes(snapshot, scheduleEntry)
  local profile = ChromaLink.Config.GetActiveProfile(snapshot.clientWidth, snapshot.clientHeight)
  local frameTypeId = (scheduleEntry and scheduleEntry.frameTypeId) or ChromaLink.Protocol.FrameTypeIds.coreStatus
  local laneId = (scheduleEntry and scheduleEntry.laneId) or ChromaLink.Protocol.LaneIds.hot
  local sequence = (scheduleEntry and scheduleEntry.sequence) or 0
  local payloadBytes, payloadLength = ChromaLink.Protocol.BuildPayloadBytesForFrame(frameTypeId, snapshot)
  local bytes = ChromaLink.Protocol.BuildTransportBytes(profile.numericId, frameTypeId, laneId, sequence, payloadBytes, payloadLength)

  return bytes, {
    profile = profile,
    frameTypeId = frameTypeId,
    laneId = laneId,
    sequence = sequence,
    payloadUsedLength = payloadLength
  }
end

function ChromaLink.Protocol.DuplicateHeaderBytes(frameBytes)
  local count = ChromaLink.Protocol.FrameLayout.duplicateHeaderBytes
  local bytes = {}
  local index

  for index = 1, count do
    bytes[index] = frameBytes[index] or 0
  end

  return bytes
end

function ChromaLink.Protocol.BuildModuleMatrix(profile, frameBytes)
  local rows = {}
  local rowIndex
  local colIndex
  local interiorColumn
  local interiorColumns = profile.gridColumns - 2
  local payloadBits = ChromaLink.Pack.BytesToBits(frameBytes)
  local duplicateBits = ChromaLink.Pack.BytesToBits(ChromaLink.Protocol.DuplicateHeaderBytes(frameBytes))
  local leftDuplicateCursor = 1
  local rightDuplicateCursor = 1
  local payloadCursor = 1

  for rowIndex = 1, profile.gridRows do
    rows[rowIndex] = {}
    for colIndex = 1, profile.gridColumns do
      rows[rowIndex][colIndex] = 0
    end
  end

  for colIndex = 1, profile.gridColumns do
    rows[1][colIndex] = 1
    if math.fmod(colIndex - 1, 2) == 0 then
      rows[profile.gridRows][colIndex] = 1
    end
  end

  for rowIndex = 1, profile.gridRows do
    rows[rowIndex][1] = 1
    if math.fmod(rowIndex - 1, 2) == 0 then
      rows[rowIndex][profile.gridColumns] = 1
    end
  end

  for rowIndex = 2, profile.gridRows - 1 do
    for interiorColumn = 1, interiorColumns do
      local absoluteColumn = interiorColumn + 1
      if interiorColumn <= profile.metadataColumnsPerSide then
        rows[rowIndex][absoluteColumn] = duplicateBits[leftDuplicateCursor] or 0
        leftDuplicateCursor = leftDuplicateCursor + 1
      elseif interiorColumn > (profile.metadataColumnsPerSide + profile.payloadColumns) then
        rows[rowIndex][absoluteColumn] = duplicateBits[rightDuplicateCursor] or 0
        rightDuplicateCursor = rightDuplicateCursor + 1
      else
        rows[rowIndex][absoluteColumn] = payloadBits[payloadCursor] or 0
        payloadCursor = payloadCursor + 1
      end
    end
  end

  return rows
end

-- end-of-script marker comment
