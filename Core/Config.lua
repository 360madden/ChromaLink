-- script name: Core/Config.lua
-- version: 0.5.0
-- purpose: Defines shared ChromaLink constants, profiles, palette placeholders, and hot-lane baseline transport sizes.
-- dependencies: None. Loaded before other ChromaLink Lua modules.
-- important assumptions: Phase 1 prioritizes a reliable monochrome-safe ChromaLink strip at 640x360 while preserving the existing ChromaLink addon identifier.
-- protocol version: ChromaLink
-- framework module role: Core configuration
-- character count note: Character count not precomputed; measure with tooling if needed.

ChromaLink = ChromaLink or {}
ChromaLink.Config = {
  addonIdentifier = "ChromaLink",
  addonVersion = "0.1.0",
  protocolName = "ChromaLink",
  protocolVersion = 2,
  requestedStrata = nil,
  requestedLayer = 100000,
  showOnStartup = true,
  chatColorHex = "#4DEAFF",
  probeLoggingEnabled = true,
  probeLoggingMaxEvents = 16,
  validationLoggingEnabled = true,
  validationLogMaxEvents = 16,
  validationSummaryMaxIssues = 4,
  castbarCacheSeconds = 0.75,
  refreshIntervalSeconds = 0.10,
  refreshIntervalCastingSeconds = 0.05,
  transportBytes = 45,
  headerBytes = 8,
  payloadBytes = 33,
  payloadCrcBytes = 4,
  duplicateHeaderBytes = 3,
  frameTypeIds = {
    coreStatus = 1,
    tactical = 2,
    calibration = 3,
    event = 4
  },
  laneIds = {
    hot = 1,
    warm = 2,
    cold = 3,
    event = 4
  },
  frameSchedulePattern = {
    {
      frameTypeId = 1,
      laneId = 1,
      name = "core-status"
    },
    {
      frameTypeId = 2,
      laneId = 1,
      name = "tactical"
    }
  },
  coordQuantizeScale = 10,
  targetRadiusQuantizeScale = 10,
  colors = {
    bandReservedDark = { 0.03, 0.03, 0.03, 1.0 },
    symbolPanelLight = { 0.96, 0.96, 0.96, 1.0 },
    moduleDark = { 0.06, 0.06, 0.06, 1.0 },
      debugAccent = { 0.16, 0.38, 0.86, 1.0 }
  },
  profiles = {
    P360A = {
      id = "P360A",
      numericId = 2,
      windowWidth = 640,
      windowHeight = 360,
      bandWidth = 640,
      bandHeight = 40,
      quietLeft = 8,
      quietRight = 8,
      quietTop = 2,
      quietBottom = 2,
      pitch = 6,
      gridColumns = 104,
      gridRows = 6,
      metadataColumnsPerSide = 6,
      payloadColumns = 90,
      payloadRows = 4,
      symbolWidth = 624,
      symbolHeight = 36
    },
    P720A = {
      id = "P720A",
      numericId = 1,
      windowWidth = 1280,
      windowHeight = 720,
      bandWidth = 1280,
      bandHeight = 64,
      quietLeft = 24,
      quietRight = 24,
      quietTop = 8,
      quietBottom = 8,
      pitch = 8,
      gridColumns = 154,
      gridRows = 6,
      dataColumns = 152,
      dataRows = 4,
      symbolWidth = 1232,
      symbolHeight = 48
    }
  }
}

function ChromaLink.Config.GetActiveProfile(clientWidth, clientHeight)
  local profiles = ChromaLink.Config.profiles

  return profiles.P360A
end

-- end-of-script marker comment
