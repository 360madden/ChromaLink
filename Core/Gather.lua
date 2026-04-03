ChromaLink = ChromaLink or {}
ChromaLink.Gather = {}

local config = ChromaLink.Config
local cachedAbilityNameIndex = nil
local cachedAbilityNameIndexStamp = 0

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
  local stateCache = ChromaLink.StateCache

  if stateCache ~= nil and stateCache.GetUnitId ~= nil then
    local cached = stateCache.GetUnitId(reference)
    if cached ~= nil then
      return cached
    end
  end

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
  local stateCache = ChromaLink.StateCache

  if stateCache ~= nil and stateCache.GetUnitDetail ~= nil then
    local cached = stateCache.GetUnitDetail(unit)
    if cached ~= nil then
      return cached
    end
  end

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
  local stateCache = ChromaLink.StateCache

  if stateCache ~= nil and stateCache.GetUnitCastbar ~= nil then
    local cached = stateCache.GetUnitCastbar(unit)
    if cached ~= nil then
      return cached
    end
  end

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
  local stateCache = ChromaLink.StateCache

  if stateCache ~= nil and stateCache.GetAbilityDetail ~= nil then
    local cached = stateCache.GetAbilityDetail(ability)
    if cached ~= nil then
      return cached
    end
  end

  if ability == nil or Inspect == nil or Inspect.Ability == nil or Inspect.Ability.New == nil or Inspect.Ability.New.Detail == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Ability.New.Detail, ability)
  if ok then
    return result
  end

  return nil
end

local function SafeAbilityList()
  local stateCache = ChromaLink.StateCache

  if stateCache ~= nil and stateCache.GetAbilityList ~= nil then
    local cached = stateCache.GetAbilityList()
    if cached ~= nil then
      return cached
    end
  end

  if Inspect == nil or Inspect.Ability == nil or Inspect.Ability.New == nil or Inspect.Ability.New.List == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Ability.New.List)
  if ok then
    return result
  end

  return nil
end

local function SafeBuffList(unit)
  local stateCache = ChromaLink.StateCache

  if stateCache ~= nil and stateCache.GetBuffList ~= nil then
    local cached = stateCache.GetBuffList(unit)
    if cached ~= nil then
      return cached
    end
  end

  if unit == nil or Inspect == nil or Inspect.Buff == nil or Inspect.Buff.List == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Buff.List, unit)
  if ok then
    return result
  end

  return nil
end

local function SafeBuffDetail(unit, buffs)
  local stateCache = ChromaLink.StateCache

  if stateCache ~= nil and stateCache.GetBuffDetail ~= nil then
    local cached = stateCache.GetBuffDetail(unit, buffs)
    if cached ~= nil then
      return cached
    end
  end

  if unit == nil or buffs == nil or Inspect == nil or Inspect.Buff == nil or Inspect.Buff.Detail == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Buff.Detail, unit, buffs)
  if ok then
    return result
  end

  return nil
end

local function SafeZoneDetail(zone)
  if zone == nil or Inspect == nil or Inspect.Zone == nil or Inspect.Zone.Detail == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Zone.Detail, zone)
  if ok then
    return result
  end

  return nil
end

local function SafeShardDetail()
  if Inspect == nil or Inspect.Shard == nil then
    return nil
  end

  local ok, result = pcall(Inspect.Shard)
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

local function BuildAsciiBytes(value, length)
  local source = string.upper(tostring(value or ""))
  local bytes = {}
  local index

  for index = 1, length do
    local byteValue = string.byte(source, index) or 32
    if byteValue < 32 or byteValue > 126 then
      byteValue = 63
    end

    bytes[index] = byteValue
  end

  return bytes
end

local function BuildSpellLabelBytes(value)
  return BuildAsciiBytes(value, 5)
end

local function BuildAuxSpellLabelBytes(value)
  return BuildAsciiBytes(value, 4)
end

local function BuildTextLabelBytes(value)
  return BuildAsciiBytes(value, 9)
end

local function StableHash16(value)
  local source = string.upper(tostring(value or ""))
  local hash = 5381
  local index

  for index = 1, #source do
    hash = math.fmod((hash * 33) + (string.byte(source, index) or 0), 65536)
  end

  return ClampUInt16(hash)
end

local function StableId16(value)
  local numeric = tonumber(value)
  if numeric ~= nil then
    return ClampUInt16(math.fmod(math.floor(math.abs(numeric) + 0.5), 65536))
  end

  return StableHash16(value)
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

local function SafeRealtimeNow()
  if Inspect ~= nil and Inspect.Time ~= nil and Inspect.Time.Real ~= nil then
    local ok, result = pcall(Inspect.Time.Real)
    if ok then
      return tonumber(result) or 0
    end
  end

  return 0
end

local function BuildRiftMeterSnapshot()
  local integrationConfig = config.riftMeter or {}
  local adapter = ChromaLink.RiftMeterAdapter

  if not integrationConfig.enabled and not integrationConfig.probeStatus then
    return nil
  end

  if adapter == nil or adapter.BuildSnapshot == nil then
    return nil
  end

  return adapter.BuildSnapshot()
end

local function AttachSourceContext(snapshot)
  local enriched = snapshot or {}
  local riftMeterSnapshot = BuildRiftMeterSnapshot()

  enriched.sourceNow = SafeRealtimeNow()
  if riftMeterSnapshot ~= nil then
    enriched.riftMeter = riftMeterSnapshot
  end

  return enriched
end

local function ClampDamageK(value)
  local number = tonumber(value) or 0
  if number < 0 then
    return 0
  end

  return ClampByte(number / 1000)
end

local function BuildAbilityNameIndex()
  local abilities = SafeAbilityList()
  local details
  local index = {}
  local abilityId
  local detail
  local name
  local normalized

  if abilities == nil then
    return index
  end

  details = SafeAbilityDetail(abilities) or {}

  for abilityId, detail in pairs(details) do
    name = detail and detail.name
    normalized = string.upper(tostring(name or ""))
    if normalized ~= "" and index[normalized] == nil then
      index[normalized] = abilityId
    end
  end

  return index
end

local function ResolveTrackedAbilityId(entry)
  local numeric = tonumber(entry)
  local normalized
  local now

  if numeric ~= nil then
    return numeric
  end

  normalized = string.upper(tostring(entry or ""))
  if normalized == "" then
    return nil
  end

  now = SafeRealtimeNow()
  if cachedAbilityNameIndex == nil or now <= 0 or (now - cachedAbilityNameIndexStamp) > 5 then
    cachedAbilityNameIndex = BuildAbilityNameIndex()
    cachedAbilityNameIndexStamp = now
  end

  return cachedAbilityNameIndex ~= nil and cachedAbilityNameIndex[normalized] or nil
end

local function NormalizeText(value, fallback)
  local text = tostring(value or fallback or "")
  if text == "" and fallback ~= nil then
    return tostring(fallback)
  end

  return text
end

local function ResolvePrimaryZoneName(player)
  local zoneDetail = SafeZoneDetail(player and player.zone)

  if zoneDetail ~= nil and zoneDetail.name ~= nil and tostring(zoneDetail.name) ~= "" then
    return tostring(zoneDetail.name)
  end

  return NormalizeText(player and player.locationName, "")
end

local function BuildTextSnapshot(kindCode, value)
  local text = NormalizeText(value, "")

  return {
    textKindCode = ClampByte(kindCode),
    textHash16 = StableHash16(text),
    textBytes = BuildTextLabelBytes(text)
  }
end

local function BuildAuraEntry(detail, wantDebuff, playerUnitId)
  local isDebuff = detail ~= nil and detail.debuff and true or false
  local infinite = detail == nil or tonumber(detail.duration) == nil or tonumber(detail.duration) <= 0
  local fromPlayer = detail ~= nil and playerUnitId ~= nil and detail.caster == playerUnitId
  local dispellable = detail ~= nil and (detail.curse or detail.disease or detail.poison)
  local flags = 0

  if detail ~= nil then
    flags = flags + 1
  end
  if wantDebuff and isDebuff then
    flags = flags + 2
  end
  if fromPlayer then
    flags = flags + 4
  end
  if dispellable then
    flags = flags + 8
  end
  if infinite then
    flags = flags + 16
  end

  return {
    id16 = StableId16((detail and detail.type) or (detail and detail.ability) or (detail and detail.name) or 0),
    remainingQ4 = infinite and 0 or QuantizeSecondsQ4(detail and detail.remaining),
    stack = ClampByte(detail and detail.stack),
    flags = ClampByte(flags),
    sortRemaining = infinite and 999999 or (tonumber(detail and detail.remaining) or 0),
    sortStack = ClampByte(detail and detail.stack)
  }
end

local function CompareAuraEntries(left, right)
  local leftFromPlayer = math.fmod(math.floor((left.flags or 0) / 4), 2)
  local rightFromPlayer = math.fmod(math.floor((right.flags or 0) / 4), 2)

  if leftFromPlayer ~= rightFromPlayer then
    return leftFromPlayer > rightFromPlayer
  end
  if left.sortRemaining ~= right.sortRemaining then
    return left.sortRemaining < right.sortRemaining
  end
  if left.sortStack ~= right.sortStack then
    return left.sortStack > right.sortStack
  end

  return left.id16 < right.id16
end

local function BuildAuraPageSnapshotForUnit(unit, pageKindCode, wantDebuff)
  local buffIds = SafeBuffList(unit)
  local details = SafeBuffDetail(unit, buffIds)
  local entries = {}
  local playerUnitId = SafeUnitLookup("player")
  local _, detail
  local totalCount = 0
  local first
  local second

  if type(details) == "table" then
    for _, detail in pairs(details) do
      local isDebuff = detail ~= nil and detail.debuff and true or false
      if isDebuff == wantDebuff then
        totalCount = totalCount + 1
        table.insert(entries, BuildAuraEntry(detail, wantDebuff, playerUnitId))
      end
    end
  end

  table.sort(entries, CompareAuraEntries)
  first = entries[1]
  second = entries[2]

  return {
    pageKindCode = ClampByte(pageKindCode),
    totalAuraCount = ClampByte(totalCount),
    entry1Id = first and first.id16 or 0,
    entry1RemainingQ4 = first and first.remainingQ4 or 0,
    entry1Stack = first and first.stack or 0,
    entry1Flags = first and first.flags or 0,
    entry2Id = second and second.id16 or 0,
    entry2RemainingQ4 = second and second.remainingQ4 or 0,
    entry2Stack = second and second.stack or 0,
    entry2Flags = second and second.flags or 0
  }
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

  return AttachSourceContext({
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
  })
end

function ChromaLink.Gather.BuildPlayerVitalsSnapshot()
  local player = SafeUnitDetail("player")
  local playerCallingCode = EncodeCallingCode(player and (player.calling or player.callingName))
  local playerResourceKind, resourceCurrent, resourceMax, _ = SelectPreferredResource(player, playerCallingCode)

  return AttachSourceContext({
    resourceKind = ClampByte(playerResourceKind),
    healthCurrent = ClampUInt32(player and player.health),
    healthMax = ClampUInt32(player and player.healthMax),
    resourceCurrent = ClampUInt16(resourceCurrent),
    resourceMax = ClampUInt16(resourceMax)
  })
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

function ChromaLink.Gather.BuildTargetVitalsSnapshot()
  local targetUnitId = SafeUnitLookup("player.target")
  local target = SafeUnitDetail(targetUnitId)

  return {
    healthCurrent = ClampUInt32(target and target.health),
    healthMax = ClampUInt32(target and target.healthMax),
    absorb = ClampUInt16(target and target.absorb),
    targetFlags = ClampByte(BuildTargetStateFlags(target)),
    targetLevel = ClampByte(target and target.level)
  }
end

function ChromaLink.Gather.BuildTargetResourcesSnapshot()
  local targetUnitId = SafeUnitLookup("player.target")
  local target = SafeUnitDetail(targetUnitId)

  return {
    manaCurrent = ClampUInt16(target and target.mana),
    manaMax = ClampUInt16(target and target.manaMax),
    energyCurrent = ClampUInt16(target and target.energy),
    energyMax = ClampUInt16(target and target.energyMax),
    powerCurrent = ClampUInt16(target and target.power),
    powerMax = ClampUInt16(target and target.powerMax)
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

function ChromaLink.Gather.BuildAuxUnitCastSnapshot(unitSelectorCode, unitSpecifier)
  local castbar = SafeUnitCastbar(unitSpecifier)
  local abilityDetail = SafeAbilityDetail(castbar and castbar.ability)
  local durationSeconds = math.max(0, tonumber(castbar and castbar.duration) or 0)
  local remainingSeconds = math.max(0, tonumber(castbar and castbar.remaining) or 0)
  local progress = 0
  local spellLabelBytes = BuildAuxSpellLabelBytes(castbar and (castbar.abilityName or castbar.ability))
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
    unitSelectorCode = ClampByte(unitSelectorCode),
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

  return AttachSourceContext({
    manaCurrent = ClampUInt16(player and player.mana),
    manaMax = ClampUInt16(player and player.manaMax),
    energyCurrent = ClampUInt16(player and player.energy),
    energyMax = ClampUInt16(player and player.energyMax),
    powerCurrent = ClampUInt16(player and player.power),
    powerMax = ClampUInt16(player and player.powerMax)
  })
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
  if player ~= nil and player.pvp then
    flags = flags + 16
  end
  if player ~= nil and player.mentoring then
    flags = flags + 32
  end
  if player ~= nil and player.ready then
    flags = flags + 64
  end
  if player ~= nil and player.afk then
    flags = flags + 128
  end

  return AttachSourceContext({
    combatFlags = ClampByte(flags),
    combo = ClampByte(player and player.combo),
    chargeCurrent = ClampUInt16(player and player.charge),
    chargeMax = ClampUInt16(player and player.chargeMax),
    planarCurrent = ClampUInt16(player and player.planar),
    planarMax = ClampUInt16(player and player.planarMax),
    absorb = ClampUInt16(player and player.absorb)
  })
end

function ChromaLink.Gather.BuildRiftMeterCombatSnapshot()
  local status = BuildRiftMeterSnapshot() or {}
  local flags = 0
  local warningCount = type(status.warnings) == "table" and #status.warnings or 0

  if status.loaded then
    flags = flags + 1
  end
  if status.available then
    flags = flags + 2
  end
  if status.inCombat then
    flags = flags + 4
  end
  if (tonumber(status.overallDamage) or 0) > 0 or (tonumber(status.overallHealing) or 0) > 0 then
    flags = flags + 8
  end
  if tonumber(status.activeCombatDurationMs) ~= nil and tonumber(status.activeCombatDurationMs) > 0 then
    flags = flags + 16
  end
  if tonumber(status.overallDurationMs) ~= nil and tonumber(status.overallDurationMs) > 0 then
    flags = flags + 32
  end
  if warningCount > 0 then
    flags = flags + 64
  elseif status.available then
    flags = flags + 128
  end

  return AttachSourceContext({
    riftMeterFlags = ClampByte(flags),
    combatCount = ClampByte(status.combatCount),
    activeCombatDurationDeci = ClampUInt16((tonumber(status.activeCombatDurationMs) or 0) / 100),
    activeCombatPlayerCount = ClampByte(status.activeCombatPlayerCount),
    activeCombatHostileCount = ClampByte(status.activeCombatHostileCount),
    overallDurationDeci = ClampUInt16((tonumber(status.overallDurationMs) or 0) / 100),
    overallPlayerCount = ClampByte(status.overallPlayerCount),
    overallHostileCount = ClampByte(status.overallHostileCount),
    overallDamageK = ClampDamageK(status.overallDamage),
    overallHealingK = ClampDamageK(status.overallHealing)
  })
end

function ChromaLink.Gather.BuildFollowUnitStatusSnapshot(slotOverride)
  local followConfig = config.followUnit or {}
  local slot = tonumber(slotOverride) or tonumber(followConfig.slot) or 1
  local specifier = followConfig.specifier or string.format("group%02d", slot)
  local detail
  local callingCode
  local roleCode
  local _, _, _, resourcePct

  if followConfig.slots ~= nil and tonumber(slotOverride) ~= nil then
    specifier = string.format("group%02d", slot)
  end

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

function ChromaLink.Gather.BuildAuraPageSnapshot(pageKindCode)
  local auraKinds = config.auraPageKinds or {}
  local targetUnitId = SafeUnitLookup("player.target")

  if pageKindCode == auraKinds.playerDebuffs then
    return BuildAuraPageSnapshotForUnit("player", pageKindCode, true)
  end
  if pageKindCode == auraKinds.targetBuffs then
    return BuildAuraPageSnapshotForUnit(targetUnitId, pageKindCode, false)
  end
  if pageKindCode == auraKinds.targetDebuffs then
    return BuildAuraPageSnapshotForUnit(targetUnitId, pageKindCode, true)
  end

  return BuildAuraPageSnapshotForUnit("player", auraKinds.playerBuffs or 1, false)
end

function ChromaLink.Gather.BuildTextPageSnapshot(textKindCode)
  local textKinds = config.textKindCodes or {}
  local player = SafeUnitDetail("player")
  local target = SafeUnitDetail(SafeUnitLookup("player.target"))
  local shard = SafeShardDetail()

  if textKindCode == textKinds.targetName then
    return BuildTextSnapshot(textKindCode, target and target.name)
  end
  if textKindCode == textKinds.zoneName then
    return BuildTextSnapshot(textKindCode, ResolvePrimaryZoneName(player))
  end
  if textKindCode == textKinds.shardName then
    return BuildTextSnapshot(textKindCode, shard and shard.name)
  end

  return BuildTextSnapshot(textKinds.playerName or 1, player and player.name)
end

function ChromaLink.Gather.BuildAbilityWatchSnapshot(pageIndex)
  local watchConfig = config.abilityWatch or {}
  local tracked = watchConfig.trackedAbilities or {}
  local totalTracked = #tracked
  local totalPages = math.max(1, math.ceil(totalTracked / 2))
  local page = math.max(1, math.min(totalPages, tonumber(pageIndex) or 1))
  local startIndex = ((page - 1) * 2) + 1
  local shortestCooldownQ4 = 0
  local readyCount = 0
  local coolingCount = 0

  local function BuildAbilityEntry(trackedEntry)
    local abilityId = ResolveTrackedAbilityId(trackedEntry)
    local detail = SafeAbilityDetail(abilityId)
    local cooldownRemaining = tonumber(detail and detail.currentCooldownRemaining) or 0
    local flags = 0

    if trackedEntry ~= nil then
      flags = flags + 1
    end
    if detail ~= nil and not detail.unusable then
      flags = flags + 2
      readyCount = readyCount + 1
    end
    if detail ~= nil and detail.outOfRange then
      flags = flags + 4
    end
    if detail ~= nil and cooldownRemaining > 0 then
      flags = flags + 8
      coolingCount = coolingCount + 1
      local cooldownQ4 = QuantizeSecondsQ4(cooldownRemaining)
      if shortestCooldownQ4 == 0 or cooldownQ4 < shortestCooldownQ4 then
        shortestCooldownQ4 = cooldownQ4
      end
    end
    if detail ~= nil and detail.currentCooldownPaused then
      flags = flags + 16
    end
    if detail ~= nil and detail.passive then
      flags = flags + 32
    end

    return {
      id16 = StableId16((detail and detail.idNew) or (detail and detail.id) or trackedEntry or 0),
      cooldownQ4 = QuantizeSecondsQ4(cooldownRemaining),
      flags = ClampByte(flags)
    }
  end

  local first = BuildAbilityEntry(tracked[startIndex])
  local second = BuildAbilityEntry(tracked[startIndex + 1])

  return {
    pageIndex = ClampByte(page),
    entry1Id = first.id16,
    entry1CooldownQ4 = first.cooldownQ4,
    entry1Flags = first.flags,
    entry2Id = second.id16,
    entry2CooldownQ4 = second.cooldownQ4,
    entry2Flags = second.flags,
    shortestCooldownQ4 = ClampByte(shortestCooldownQ4),
    readyCount = ClampByte(readyCount),
    coolingCount = ClampByte(coolingCount)
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

function ChromaLink.Gather.BuildSyntheticRiftMeterCombatSnapshot()
  return {
    riftMeterFlags = 0xBF,
    combatCount = 2,
    activeCombatDurationDeci = 123,
    activeCombatPlayerCount = 1,
    activeCombatHostileCount = 3,
    overallDurationDeci = 456,
    overallPlayerCount = 5,
    overallHostileCount = 8,
    overallDamageK = 42,
    overallHealingK = 9
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
    combatFlags = 0xFF,
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

function ChromaLink.Gather.BuildSyntheticTargetVitalsSnapshot()
  return {
    healthCurrent = 18250,
    healthMax = 20000,
    absorb = 640,
    targetFlags = 15,
    targetLevel = 72
  }
end

function ChromaLink.Gather.BuildSyntheticTargetResourcesSnapshot()
  return {
    manaCurrent = 1200,
    manaMax = 2000,
    energyCurrent = 0,
    energyMax = 0,
    powerCurrent = 35,
    powerMax = 100
  }
end

function ChromaLink.Gather.BuildSyntheticAuxUnitCastSnapshot(unitSelectorCode)
  return {
    unitSelectorCode = ClampByte(unitSelectorCode or config.unitSelectorCodes.target),
    castFlags = 25,
    progressPctQ8 = 112,
    durationCenti = 320,
    remainingCenti = 140,
    castTargetCode = config.castTargetCodes.currentTarget,
    spellLabelBytes = BuildAuxSpellLabelBytes("BOLT")
  }
end

function ChromaLink.Gather.BuildSyntheticAuraPageSnapshot(pageKindCode)
  return {
    pageKindCode = ClampByte(pageKindCode or (config.auraPageKinds.playerBuffs)),
    totalAuraCount = 4,
    entry1Id = StableId16("SYNTH-AURA-1"),
    entry1RemainingQ4 = 24,
    entry1Stack = 3,
    entry1Flags = 1 + 4,
    entry2Id = StableId16("SYNTH-AURA-2"),
    entry2RemainingQ4 = 12,
    entry2Stack = 1,
    entry2Flags = 1
  }
end

function ChromaLink.Gather.BuildSyntheticTextPageSnapshot(textKindCode)
  local textKinds = config.textKindCodes or {}

  if textKindCode == textKinds.targetName then
    return BuildTextSnapshot(textKindCode, "TARGETORC")
  end
  if textKindCode == textKinds.zoneName then
    return BuildTextSnapshot(textKindCode, "SILVERWD")
  end
  if textKindCode == textKinds.shardName then
    return BuildTextSnapshot(textKindCode, "GREYBRIA")
  end

  return BuildTextSnapshot(textKinds.playerName or 1, "RIFTLEAD")
end

function ChromaLink.Gather.BuildSyntheticAbilityWatchSnapshot(pageIndex)
  return {
    pageIndex = ClampByte(pageIndex or 1),
    entry1Id = StableId16("HEAL"),
    entry1CooldownQ4 = 6,
    entry1Flags = 1 + 2 + 8,
    entry2Id = StableId16("SHLD"),
    entry2CooldownQ4 = 0,
    entry2Flags = 1 + 2,
    shortestCooldownQ4 = 6,
    readyCount = 1,
    coolingCount = 1
  }
end

function ChromaLink.Gather.BuildSyntheticFollowUnitStatusSnapshot(slotOverride)
  local slot = ClampByte(slotOverride or 1)

  return {
    slot = slot,
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
