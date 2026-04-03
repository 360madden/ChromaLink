ChromaLink = ChromaLink or {}
ChromaLink.RiftMeterAdapter = {}

local function SafeNow()
  if Inspect ~= nil and Inspect.Time ~= nil and Inspect.Time.Real ~= nil then
    local ok, result = pcall(Inspect.Time.Real)
    if ok then
      return tonumber(result) or 0
    end
  end

  return 0
end

local function SafeFrameNow()
  if Inspect ~= nil and Inspect.Time ~= nil and Inspect.Time.Frame ~= nil then
    local ok, result = pcall(Inspect.Time.Frame)
    if ok then
      return tonumber(result) or 0
    end
  end

  return 0
end

local function SafeCountEntries(value)
  local count = 0
  local _

  if type(value) ~= "table" then
    return 0
  end

  for _ in pairs(value) do
    count = count + 1
  end

  return count
end

local function SafeSortedKeys(value, limit)
  local keys = {}
  local capped = math.max(1, tonumber(limit) or 8)
  local key

  if type(value) ~= "table" then
    return keys
  end

  for key in pairs(value) do
    table.insert(keys, tostring(key))
  end

  table.sort(keys)

  while #keys > capped do
    table.remove(keys)
  end

  return keys
end

local function SafeLatestCombat(riftMeter)
  local combats
  local count

  if type(riftMeter) ~= "table" then
    return nil, 0
  end

  combats = riftMeter.combats
  if type(combats) ~= "table" then
    return nil, 0
  end

  count = #combats
  if count > 0 then
    return combats[count], count
  end

  return nil, SafeCountEntries(combats)
end

local function SafeNumber(value)
  local number = tonumber(value)
  if number == nil then
    return nil
  end

  return number
end

local function SafeDurationMs(durationSeconds, startTimeFrame, inCombat)
  local durationNumber = SafeNumber(durationSeconds)
  local startTimeNumber = SafeNumber(startTimeFrame)
  local frameNow

  if durationNumber ~= nil and durationNumber > 0 then
    return math.max(0, math.floor((durationNumber * 1000) + 0.5))
  end

  if inCombat and startTimeNumber ~= nil then
    frameNow = SafeFrameNow()
    if frameNow > 0 and frameNow >= startTimeNumber then
      return math.max(0, math.floor(((frameNow - startTimeNumber) * 1000) + 0.5))
    end
  end

  return nil
end

local function BuildSnapshot()
  local now = SafeNow()
  local riftMeter = _G.RiftMeter
  local latestCombat
  local combatCount
  local overall
  local snapshot = {
    loaded = false,
    available = false,
    inCombat = false,
    combatCount = 0,
    activeCombatDurationMs = nil,
    activeCombatPlayerCount = 0,
    activeCombatHostileCount = 0,
    overallDamage = nil,
    overallHealing = nil,
    overallDurationMs = nil,
    overallPlayerCount = 0,
    overallHostileCount = 0,
    sampledAt = now,
    topLevelKeys = {},
    combatKeys = {},
    overallKeys = {},
    warnings = {}
  }

  if type(riftMeter) ~= "table" then
    table.insert(snapshot.warnings, "RiftMeter global unavailable")
    return snapshot
  end

  snapshot.loaded = true
  snapshot.available = true
  snapshot.topLevelKeys = SafeSortedKeys(riftMeter, 16)

  latestCombat, combatCount = SafeLatestCombat(riftMeter)
  snapshot.combatCount = combatCount or 0
  if type(latestCombat) == "table" then
    snapshot.inCombat = latestCombat.ended ~= true
    snapshot.combatKeys = SafeSortedKeys(latestCombat, 16)
    snapshot.activeCombatPlayerCount = SafeCountEntries(latestCombat.players)
    snapshot.activeCombatHostileCount = SafeCountEntries(latestCombat.hostiles)
    snapshot.activeCombatDurationMs = SafeDurationMs(latestCombat.duration, latestCombat.startTime, snapshot.inCombat)
  else
    table.insert(snapshot.warnings, "No combat snapshot available")
  end

  overall = riftMeter.overall
  if type(overall) == "table" then
    snapshot.overallKeys = SafeSortedKeys(overall, 16)
    snapshot.overallDamage = SafeNumber(overall.damage)
    snapshot.overallHealing = SafeNumber(overall.healing)
    snapshot.overallPlayerCount = SafeCountEntries(overall.players)
    snapshot.overallHostileCount = SafeCountEntries(overall.hostiles)
    snapshot.overallDurationMs = SafeDurationMs(overall.duration, overall.startTime, false)
  else
    table.insert(snapshot.warnings, "Overall summary unavailable")
  end

  return snapshot
end

function ChromaLink.RiftMeterAdapter.IsLoaded()
  return type(_G.RiftMeter) == "table"
end

function ChromaLink.RiftMeterAdapter.BuildSnapshot()
  return BuildSnapshot()
end
