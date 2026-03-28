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
  protocolVersion = 1,
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
    payloadSymbolCount = 64
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
  }
}

function ChromaLink.Config.GetPaletteColor(symbol)
  return ChromaLink.Config.palette[symbol] or ChromaLink.Config.palette[0]
end
