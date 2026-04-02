ChromaLink = ChromaLink or {}
ChromaLink.Gather = {}

local config = ChromaLink.Config

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

local function ClampUInt16(value)
  local number = tonumber(value) or 0
  if number < 0 then
    return 0
  end
  if number > 65535 then
    return 65535
  end
  return math.floor(number + 0.5)
end

local function ClampUInt32(value)
  local number = tonumber(value) or 0
  if number < 0 then
    return 0
  end
  if number > 4294967295 then
    return 4294967295
  end
  return math.floor(number + 0.5)
end

local function Lower(value)
  if value == nil then
    return ""
  end
  return string.lower(tostring(value))
end

local function SafeUnitLookup(reference)
  if Inspect == nil or Inspect.Unit == nil or Inspect.Unit.Lookup == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Unit.Lookup, reference)
  if ok then
    return result
  end

  return nil
end

local function SafeUnitDetail(unit)
  if unit == nil or Inspect == nil or Inspect.Unit == nil or Inspect.Unit.Detail == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Unit.Detail, unit)
  if ok then
    return result
  end

  return nil
end

local function SafeUnitCastbar(unit)
  if unit == nil or Inspect == nil or Inspect.Unit == nil or Inspect.Unit.Castbar == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Unit.Castbar, unit)
  if ok then
    return result
  end

  return nil
end

local function SafeAbilityDetail(ability)
  if ability == nil or Inspect == nil or Inspect.Ability == nil or Inspect.Ability.New == nil or Inspect.Ability.New.Detail == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Ability.New.Detail, ability)
  if ok then
    return result
  end

  return nil
end

local function QuantizePercent(current, maximum)
  local currentNumber = tonumber(current) or 0
  local maxNumber = tonumber(maximum) or 0

  if maxNumber <= 0 then
    return 0
  end

  return ClampByte((currentNumber / maxNumber) * 255)
end

local function QuantizeSecondsQ4(seconds)
  local value = tonumber(seconds) or 0
  if value < 0 then
    return 0
  end

  return ClampByte(value * 4)
end

local function QuantizeSecondsCenti(seconds)
  local value = tonumber(seconds) or 0
  if value < 0 then
    return 0
  end

  return ClampUInt16(value * 100)
end

local function BuildSpellLabelBytes(value)
  local source = string.upper(tostring(value or ""))
  local bytes = {}
  local index

  for index = 1, 5 do
    local byteValue = string.byte(source, index) or 32
    if byteValue < 32 or byteValue > 126 then
      byteValue = 63
    end

    bytes[index] = byteValue
  end

  return bytes
end

local function HasVisibleLabel(labelBytes)
  local index

  for index = 1, #labelBytes do
    if (labelBytes[index] or 32) ~= 32 then
      return true
    end
  end

  return false
end

local function BuildPackedFollowFlags(detail)
  local flags = 0

  if detail ~= nil then
    flags = flags + 1
  end
  if detail ~= nil and not detail.dead then
    flags = flags + 2
  end
  if detail ~= nil and detail.combat then
    flags = flags + 4
  end
  if detail ~= nil and detail.afk then
    flags = flags + 8
  end
  if detail ~= nil and detail.offline then
    flags = flags + 16
  end
  if detail ~= nil and detail.aggro then
    flags = flags + 32
  end
  if detail ~= nil and detail.blocked then
    flags = flags + 64
  end
  if detail ~= nil and detail.ready then
    flags = flags + 128
  end

  return ClampByte(flags)
end

local function EncodeCallingCode(value)
  local text = Lower(value)
  if string.find(text, "warrior", 1, true) then
    return config.callingCodes.warrior
  end
  if string.find(text, "cleric", 1, true) then
    return config.callingCodes.cleric
  end
  if string.find(text, "mage", 1, true) then
    return config.callingCodes.mage
  end
  if string.find(text, "rogue", 1, true) then
    return config.callingCodes.rogue
  end
  if string.find(text, "primalist", 1, true) then
    return config.callingCodes.primalist
  end
  return 0
end

local function EncodeRoleCode(value)
  local text = Lower(value)
  if string.find(text, "tank", 1, true) then
    return config.roleCodes.tank
  end
  if string.find(text, "heal", 1, true) then
    return config.roleCodes.healer
  end
  if string.find(text, "support", 1, true) then
    return config.roleCodes.support
  end
  if string.find(text, "dps", 1, true) or string.find(text, "damage", 1, true) then
    return config.roleCodes.dps
  end
  return config.roleCodes.unknown
end

local function EncodeRelationCode(value, targetUnitId)
  if targetUnitId == "player" then
    return config.relationCodes.self
  end

  local text = Lower(value)
  if string.find(text, "hostile", 1, true) or string.find(text, "enemy", 1, true) then
    return config.relationCodes.hostile
  end
  if string.find(text, "friendly", 1, true) or string.find(text, "ally", 1, true) then
    return config.relationCodes.friendly
  end
  if string.find(text, "neutral", 1, true) then
    return config.relationCodes.neutral
  end
  return config.relationCodes.unknown
end

local function SelectPreferredResource(detail, callingCode)
  if detail == nil then
    return config.resourceKinds.none, 0, 0, 0
  end

  local candidates = {
    { kind = config.resourceKinds.mana, current = detail.mana, maximum = detail.manaMax },
    { kind = config.resourceKinds.energy, current = detail.energy, maximum = detail.energyMax },
    { kind = config.resourceKinds.power, current = detail.power, maximum = detail.powerMax },
    { kind = config.resourceKinds.charge, current = detail.charge, maximum = detail.chargeMax },
    { kind = config.resourceKinds.planar, current = detail.planar, maximum = detail.planarMax }
  }

  local preferredKind = config.resourceKinds.none
  if callingCode == config.callingCodes.rogue then
    preferredKind = config.resourceKinds.energy
  elseif callingCode == config.callingCodes.warrior then
    preferredKind = config.resourceKinds.power
  else
    preferredKind = config.resourceKinds.mana
  end

  local index
  for index = 1, #candidates do
    local candidate = candidates[index]
    if candidate.kind == preferredKind and tonumber(candidate.maximum) ~= nil and tonumber(candidate.maximum) > 0 then
      return candidate.kind, ClampUInt16(candidate.current), ClampUInt16(candidate.maximum), QuantizePercent(candidate.current, candidate.maximum)
    end
  end

  for index = 1, #candidates do
    local candidate = candidates[index]
    if tonumber(candidate.maximum) ~= nil and tonumber(candidate.maximum) > 0 then
      return candidate.kind, ClampUInt16(candidate.current), ClampUInt16(candidate.maximum), QuantizePercent(candidate.current, candidate.maximum)
    end
  end

  return config.resourceKinds.none, 0, 0, 0
end

local function BuildPlayerStateFlags(detail)
  local flags = 0
  if detail ~= nil then
    flags = flags + 1
  end
  if detail ~= nil and not detail.dead then
    flags = flags + 2
  end
  if detail ~= nil and detail.combat then
    flags = flags + 4
  end
  return flags
end

local function BuildTargetStateFlags(detail)
  local flags = 0
  if detail ~= nil then
    flags = flags + 1
  end
  if detail ~= nil and not detail.dead then
    flags = flags + 2
  end
  if detail ~= nil and detail.combat then
    flags = flags + 4
  end
  if detail ~= nil and (detail.tagged or detail.marked) then
    flags = flags + 8
  end
  return flags
end

local function EncodeCastTargetCode(targetUnitId)
  local castTargetCodes = config.castTargetCodes or {}
  local slot
  local groupSpecifier
  local resolvedId
  local targetDetail

  if targetUnitId == nil or targetUnitId == "" then
    return ClampByte(castTargetCodes.none or 0)
  end

  if targetUnitId == SafeUnitLookup("player") then
    return ClampByte(castTargetCodes.self or 1)
  end

  if targetUnitId == SafeUnitLookup("player.target") then
    return ClampByte(castTargetCodes.currentTarget or 2)
  end

  if targetUnitId == SafeUnitLookup("focus") then
    return ClampByte(castTargetCodes.focus or 3)
  end

  for slot = 1, 20 do
    groupSpecifier = string.format("group%02d", slot)
    resolvedId = SafeUnitLookup(groupSpecifier)
    if resolvedId ~= nil and resolvedId == targetUnitId then
      return ClampByte((castTargetCodes.groupBase or 4) + (slot - 1))
    end
  end

  targetDetail = SafeUnitDetail(targetUnitId)
  if targetDetail ~= nil and Lower(targetDetail.relation) == "friendly" then
    return ClampByte(castTargetCodes.friendlyOther or 24)
  end
  if targetDetail ~= nil and Lower(targetDetail.relation) == "hostile" then
    return ClampByte(castTargetCodes.hostileOther or 25)
  end

  return ClampByte(castTargetCodes.other or 26)
end

function ChromaLink.Gather.BuildCoreStatusSnapshot()
  local player = SafeUnitDetail("player")
  local targetUnitId = SafeUnitLookup("player.target")
  local target = SafeUnitDetail(targetUnitId)
  local playerCallingCode = EncodeCallingCode(player and (player.calling or player.callingName))
  local targetCallingCode = EncodeCallingCode(target and (target.calling or target.callingName))
  local playerRoleCode = EncodeRoleCode(player and (player.role or player.roleName or player.playstyle))
  local relationCode = EncodeRelationCode(target and target.relation, targetUnitId)
  local playerResourceKind, _, _, playerResourcePct = SelectPreferredResource(player, playerCallingCode)
  local targetResourceKind, _, _, targetResourcePct = SelectPreferredResource(target, targetCallingCode)

  return {
    playerStateFlags = ClampByte(BuildPlayerStateFlags(player)),
    playerHealthPctQ8 = ClampByte(QuantizePercent(player and player.health, player and player.healthMax)),
    playerResourceKind = ClampByte(playerResourceKind),
    playerResourcePctQ8 = ClampByte(playerResourcePct),
    targetStateFlags = ClampByte(BuildTargetStateFlags(target)),
    targetHealthPctQ8 = ClampByte(QuantizePercent(target and target.health, target and target.healthMax)),
    targetResourceKind = ClampByte(targetResourceKind),
    targetResourcePctQ8 = ClampByte(targetResourcePct),
    playerLevel = ClampByte(player and player.level),
    targetLevel = ClampByte(target and target.level),
    playerCallingRolePacked = ClampByte((playerCallingCode * 16) + playerRoleCode),
    targetCallingRelationPacked = ClampByte((targetCallingCode * 16) + relationCode)
  }
end

function ChromaLink.Gather.BuildPlayerVitalsSnapshot()
  local player = SafeUnitDetail("player")
  local playerCallingCode = EncodeCallingCode(player and (player.calling or player.callingName))
  local playerResourceKind, resourceCurrent, resourceMax, _ = SelectPreferredResource(player, playerCallingCode)

  return {
    resourceKind = ClampByte(playerResourceKind),
    healthCurrent = ClampUInt32(player and player.health),
    healthMax = ClampUInt32(player and player.healthMax),
    resourceCurrent = ClampUInt16(resourceCurrent),
    resourceMax = ClampUInt16(resourceMax)
  }
end

function ChromaLink.Gather.BuildPlayerPositionSnapshot()
  local player = SafeUnitDetail("player")

  return {
    x = tonumber(player and player.coordX) or 0,
    y = tonumber(player and player.coordY) or 0,
    z = tonumber(player and player.coordZ) or 0
  }
end

function ChromaLink.Gather.BuildTargetPositionSnapshot()
  local targetUnitId = SafeUnitLookup("player.target")
  local target = SafeUnitDetail(targetUnitId)

  return {
    x = tonumber(target and target.coordX) or 0,
    y = tonumber(target and target.coordY) or 0,
    z = tonumber(target and target.coordZ) or 0
  }
end

function ChromaLink.Gather.BuildPlayerCastSnapshot()
  local castbar = SafeUnitCastbar("player")
  local abilityDetail = SafeAbilityDetail(castbar and castbar.ability)
  local durationSeconds = math.max(0, tonumber(castbar and castbar.duration) or 0)
  local remainingSeconds = math.max(0, tonumber(castbar and castbar.remaining) or 0)
  local progress = 0
  local spellLabelBytes = BuildSpellLabelBytes(castbar and (castbar.abilityName or castbar.ability))
  local flags = 0

  if durationSeconds > 0 then
    local completedSeconds = durationSeconds - remainingSeconds
    if completedSeconds < 0 then
      completedSeconds = 0
    end

    progress = ClampByte((completedSeconds / durationSeconds) * 255)
  end

  if castbar ~= nil and (durationSeconds > 0 or remainingSeconds > 0 or HasVisibleLabel(spellLabelBytes)) then
    flags = flags + 1
  end
  if castbar ~= nil and castbar.channeled then
    flags = flags + 2
  end
  if castbar ~= nil and castbar.uninterruptible then
    flags = flags + 4
  end
  if HasVisibleLabel(spellLabelBytes) then
    flags = flags + 8
  end
  if abilityDetail ~= nil and abilityDetail.target ~= nil and abilityDetail.target ~= "" then
    flags = flags + 16
  end

  return {
    castFlags = ClampByte(flags),
    progressPctQ8 = ClampByte(progress),
    durationCenti = QuantizeSecondsCenti(durationSeconds),
    remainingCenti = QuantizeSecondsCenti(remainingSeconds),
    castTargetCode = EncodeCastTargetCode(abilityDetail and abilityDetail.target),
    spellLabelBytes = spellLabelBytes
  }
end

function ChromaLink.Gather.BuildPlayerResourcesSnapshot()
  local player = SafeUnitDetail("player")

  return {
    manaCurrent = ClampUInt16(player and player.mana),
    manaMax = ClampUInt16(player and player.manaMax),
    energyCurrent = ClampUInt16(player and player.energy),
    energyMax = ClampUInt16(player and player.energyMax),
    powerCurrent = ClampUInt16(player and player.power),
    powerMax = ClampUInt16(player and player.powerMax)
  }
end

function ChromaLink.Gather.BuildPlayerCombatSnapshot()
  local player = SafeUnitDetail("player")
  local flags = 0

  if player ~= nil and tonumber(player.combo) ~= nil then
    flags = flags + 1
  end
  if player ~= nil and tonumber(player.chargeMax) ~= nil and tonumber(player.chargeMax) > 0 then
    flags = flags + 2
  end
  if player ~= nil and tonumber(player.planarMax) ~= nil and tonumber(player.planarMax) > 0 then
    flags = flags + 4
  end
  if player ~= nil and tonumber(player.absorb) ~= nil and tonumber(player.absorb) > 0 then
    flags = flags + 8
  end

  return {
    combatFlags = ClampByte(flags),
    combo = ClampByte(player and player.combo),
    chargeCurrent = ClampUInt16(player and player.charge),
    chargeMax = ClampUInt16(player and player.chargeMax),
    planarCurrent = ClampUInt16(player and player.planar),
    planarMax = ClampUInt16(player and player.planarMax),
    absorb = ClampUInt16(player and player.absorb)
  }
end

function ChromaLink.Gather.BuildFollowUnitStatusSnapshot()
  local followConfig = config.followUnit or {}
  local slot = tonumber(followConfig.slot) or 1
  local specifier = followConfig.specifier or string.format("group%02d", slot)
  local detail
  local callingCode
  local roleCode
  local _, _, _, resourcePct

  if not followConfig.enabled then
    return {
      slot = 0,
      followFlags = 0,
      xQ2 = 0,
      yQ2 = 0,
      zQ2 = 0,
      healthPctQ8 = 0,
      resourcePctQ8 = 0,
      level = 0,
      callingRolePacked = 0
    }
  end

  detail = SafeUnitDetail(specifier)
  callingCode = EncodeCallingCode(detail and (detail.calling or detail.callingName))
  roleCode = EncodeRoleCode(detail and (detail.role or detail.roleName or detail.playstyle))
  _, _, _, resourcePct = SelectPreferredResource(detail, callingCode)

  return {
    slot = detail ~= nil and ClampByte(slot) or 0,
    followFlags = BuildPackedFollowFlags(detail),
    xQ2 = tonumber(detail and detail.coordX) or 0,
    yQ2 = tonumber(detail and detail.coordY) or 0,
    zQ2 = tonumber(detail and detail.coordZ) or 0,
    healthPctQ8 = ClampByte(QuantizePercent(detail and detail.health, detail and detail.healthMax)),
    resourcePctQ8 = ClampByte(resourcePct),
    level = ClampByte(detail and detail.level),
    callingRolePacked = ClampByte((callingCode * 16) + roleCode)
  }
end

function ChromaLink.Gather.BuildSyntheticCoreStatusSnapshot()
  return {
    playerStateFlags = 7,
    playerHealthPctQ8 = 198,
    playerResourceKind = config.resourceKinds.mana,
    playerResourcePctQ8 = 144,
    targetStateFlags = 15,
    targetHealthPctQ8 = 91,
    targetResourceKind = config.resourceKinds.none,
    targetResourcePctQ8 = 0,
    playerLevel = 70,
    targetLevel = 72,
    playerCallingRolePacked = 49,
    targetCallingRelationPacked = 66
  }
end

function ChromaLink.Gather.BuildSyntheticPlayerVitalsSnapshot()
  return {
    resourceKind = config.resourceKinds.energy,
    healthCurrent = 3260,
    healthMax = 3260,
    resourceCurrent = 100,
    resourceMax = 100
  }
end

function ChromaLink.Gather.BuildSyntheticPlayerPositionSnapshot()
  return {
    x = 123.45,
    y = 200.67,
    z = -50.12
  }
end

function ChromaLink.Gather.BuildSyntheticPlayerCastSnapshot()
  return {
    castFlags = 25,
    progressPctQ8 = 96,
    durationCenti = 250,
    remainingCenti = 150,
    castTargetCode = config.castTargetCodes.currentTarget,
    spellLabelBytes = BuildSpellLabelBytes("HEALING")
  }
end

function ChromaLink.Gather.BuildSyntheticPlayerResourcesSnapshot()
  return {
    manaCurrent = 4200,
    manaMax = 5000,
    energyCurrent = 85,
    energyMax = 100,
    powerCurrent = 12,
    powerMax = 100
  }
end

function ChromaLink.Gather.BuildSyntheticPlayerCombatSnapshot()
  return {
    combatFlags = 15,
    combo = 4,
    chargeCurrent = 80,
    chargeMax = 100,
    planarCurrent = 3,
    planarMax = 6,
    absorb = 250
  }
end

function ChromaLink.Gather.BuildSyntheticTargetPositionSnapshot()
  return {
    x = 128.75,
    y = 201.5,
    z = -48.25
  }
end

function ChromaLink.Gather.BuildSyntheticFollowUnitStatusSnapshot()
  return {
    slot = 1,
    followFlags = 143,
    xQ2 = 7123.5,
    yQ2 = 865.0,
    zQ2 = 3010.5,
    healthPctQ8 = 222,
    resourcePctQ8 = 144,
    level = 70,
    callingRolePacked = 0x31
  }
end
