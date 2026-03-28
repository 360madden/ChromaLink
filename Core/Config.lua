ChromaLink = ChromaLink or {}
ChromaLink.Config = {
  addonIdentifier = "ChromaLink",
  addonVersion = "0.1.0",
  showOnStartup = true,
  chatColorHex = "#65D6FF",
  requestedLayer = 100000,
  requestedStrata = nil,
  tickIntervalSeconds = 0.20,
  band = {
    minWidth = 320,
    height = 24,
    top = 0,
    left = 0,
    padding = 4,
    moduleCount = 20,
    moduleSize = 8,
    moduleGap = 4,
    pulseTail = 4
  },
  colors = {
    bandBackground = { 0.05, 0.07, 0.10, 0.95 },
    moduleOff = { 0.16, 0.22, 0.28, 0.95 },
    moduleOn = { 0.19, 0.87, 0.85, 1.0 },
    moduleAccent = { 0.96, 0.66, 0.20, 1.0 }
  }
}

function ChromaLink.Config.GetBandWidth(clientWidth)
  local width = tonumber(clientWidth) or 0

  if width < ChromaLink.Config.band.minWidth then
    return ChromaLink.Config.band.minWidth
  end

  return math.floor(width + 0.5)
end
