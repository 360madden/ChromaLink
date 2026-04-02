ChromaLink = ChromaLink or {}

local function Rgba(r, g, b)
  return { r / 255, g / 255, b / 255, 1.0 }
end

ChromaLink.Config = {
  addonIdentifier = "ChromaLink",
  addonVersion = "0.1.0",
  requestedLayer = 100000,
  requestedStrata = "topmost",
  showOnStartup = true,
  refreshIntervalSeconds = 0.10,
  stripOffsetX = 0,
  stripOffsetY = 0,
  layoutDiagnostics = {
    enabled = false,
    logEvents = false,
    maxTraceEventsPerFrame = 12,
    anchorContextToNativeTopLevel = false,
    reparentRootToNativeTopLevel = false,
    probeBarOffsetX = 0,
    probeBarOffsetY = 32,
    probeBarHeight = 12,
    probeMarkerWidth = 20,
    probeMarkerSymbols = { 6, 5, 7, 3, 2 }
  },
  quietZone = {
    enabled = false,
    fullAnchorWidth = true,
    height = 48,
    backgroundSymbol = 0
  },
  observerLane = {
    enabled = false,
    offsetY = 32,
    height = 12,
    backgroundColor = { 0.45, 0.45, 0.45, 0.95 },
    markerWidth = 20,
    markerSymbols = { 0, 1, 2, 3, 4, 5, 6, 7 }
  },
  displayCompensation = {
    enabled = false,
    mode = "anchor-ratio",
    maxScaleX = 4.0,
    maxScaleY = 4.0,
    allowShrink = false
  },
  followUnit = {
    enabled = true,
    slot = 1,
    specifier = "group01",
    slots = { 1 }
  },
  abilityWatch = {
    enabled = true,
    trackedAbilities = {}
  },
  preferHighestAvailableStrata = true,
  protocolVersion = 1,
  headerFlags = {
    multiFrameRotation = 1,
    playerPosition = 2,
    playerCast = 4,
    expandedStats = 8,
    targetPosition = 16,
    followUnitStatus = 32,
    additionalTelemetry = 64,
    textAndAuras = 128
  },
  frameTypes = {
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
  },
  frameRotation = {
    "coreStatus",
    "coreStatus",
    "playerVitals",
    "coreStatus",
    "playerPosition",
    "coreStatus",
    "playerCast",
    "coreStatus",
    "playerResources",
    "coreStatus",
    "playerCombat",
    "coreStatus",
    "targetVitals",
    "coreStatus",
    "targetResources",
    "coreStatus",
    "targetPosition",
    "coreStatus",
    "auxUnitCast",
    "coreStatus",
    "followUnitStatus",
    "coreStatus",
    "auraPage",
    "coreStatus",
    "auraPage",
    "coreStatus",
    "textPage",
    "coreStatus",
    "textPage",
    "coreStatus",
    "abilityWatch"
  },
  syntheticMode = {
    enabled = false
  },
  profile = {
    id = "P360C",
    numericId = 1,
    windowWidth = 640,
    windowHeight = 360,
    bandWidth = 640,
    bandHeight = 24,
    segmentCount = 80,
    segmentWidth = 8,
    segmentHeight = 24,
    payloadStartIndex = 9,
    payloadSymbolCount = 64,
    displayScaleX = 1.0,
    displayScaleY = 1.0
  },
  controlLeft = { 0, 1, 0, 1, 2, 3, 4, 5 },
  controlRight = { 5, 4, 3, 2, 1, 0, 1, 0 },
  palette = {
    [0] = Rgba(16, 16, 16),
    [1] = Rgba(245, 245, 245),
    [2] = Rgba(255, 59, 48),
    [3] = Rgba(52, 199, 89),
    [4] = Rgba(10, 132, 255),
    [5] = Rgba(255, 214, 10),
    [6] = Rgba(191, 90, 242),
    [7] = Rgba(100, 210, 255)
  },
  resourceKinds = {
    none = 0,
    mana = 1,
    energy = 2,
    power = 3,
    charge = 4,
    planar = 5
  },
  callingCodes = {
    warrior = 1,
    cleric = 2,
    mage = 3,
    rogue = 4,
    primalist = 5
  },
  roleCodes = {
    unknown = 0,
    dps = 1,
    tank = 2,
    healer = 3,
    support = 4
  },
  relationCodes = {
    unknown = 0,
    friendly = 1,
    hostile = 2,
    neutral = 3,
    self = 4
  },
  castTargetCodes = {
    none = 0,
    self = 1,
    currentTarget = 2,
    focus = 3,
    groupBase = 4,
    groupMax = 23,
    friendlyOther = 24,
    hostileOther = 25,
    other = 26
  },
  unitSelectorCodes = {
    none = 0,
    target = 1,
    groupBase = 16
  },
  auraPageKinds = {
    playerBuffs = 1,
    playerDebuffs = 2,
    targetBuffs = 3,
    targetDebuffs = 4
  },
  textKindCodes = {
    playerName = 1,
    targetName = 2,
    zoneName = 3,
    shardName = 4
  }
}

function ChromaLink.Config.GetPaletteColor(symbol)
  return ChromaLink.Config.palette[symbol] or ChromaLink.Config.palette[0]
end
