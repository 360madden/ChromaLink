ChromaLink = ChromaLink or {}
ChromaLink.Render = {}

local config = ChromaLink.Config
local function ApplyColor(frame, color)
  frame:SetBackgroundColor(color[1], color[2], color[3], color[4])
end

local function ReadDimension(frame, methodName, fallback)
  if frame == nil or frame[methodName] == nil then
    return fallback
  end

  local ok, value = pcall(function()
    return frame[methodName](frame)
  end)

  value = tonumber(value) or fallback
  if not ok or value == nil or value <= 0 then
    return fallback
  end

  return value
end

local function ResolveStripCount(profile)
  return math.max(1, math.floor(tonumber(profile and profile.stripCount) or 1))
end

local function ResolveTotalBandHeight(profile, layoutScaleY)
  return ResolveStripCount(profile) * profile.bandHeight * layoutScaleY
end

local function ResolveObserverLaneTop(profile)
  local observerConfig = config.observerLane or {}
  local requestedTop = tonumber(observerConfig.offsetY) or 32
  local stripBase = tonumber(profile.bandHeight) or 0
  local carryForward = math.max(0, requestedTop - stripBase)
  return ResolveStripCount(profile) * stripBase + carryForward
end

local function CreateObserverLane(rootFrame, profile)
  local observerConfig = config.observerLane
  if observerConfig == nil then
    return nil
  end

  local probeBar = UI.CreateFrame("Frame", "ChromaLinkObserverLane", rootFrame)
  local markers = {}
  local symbols = observerConfig.markerSymbols or {}
  local backgroundColor = observerConfig.backgroundColor or { 0.45, 0.45, 0.45, 0.95 }

  probeBar:SetPoint(
    "TOPLEFT",
    rootFrame,
    "TOPLEFT",
    0,
    ResolveObserverLaneTop(profile))
  probeBar:SetWidth(profile.bandWidth)
  probeBar:SetHeight(observerConfig.height or 8)
  probeBar:SetLayer(config.requestedLayer + 2)
  ApplyColor(probeBar, backgroundColor)
  probeBar:SetVisible(observerConfig.enabled and true or false)

  for index, symbol in ipairs(symbols) do
    local marker = UI.CreateFrame("Frame", "ChromaLinkObserverMarker" .. tostring(index), probeBar)
    marker:SetLayer(config.requestedLayer + 3)
    ApplyColor(marker, config.GetPaletteColor(symbol))
    marker:SetVisible(observerConfig.enabled and true or false)
    markers[index] = {
      frame = marker,
      fraction = (#symbols > 1) and ((index - 1) / (#symbols - 1)) or 0
    }
  end

  return {
    bar = probeBar,
    markers = markers,
    enabled = observerConfig.enabled and true or false
  }
end

local function CreateQuietZone(rootFrame)
  local quietZoneConfig = config.quietZone
  if quietZoneConfig == nil or not quietZoneConfig.enabled then
    return nil
  end

  local quietZone = UI.CreateFrame("Frame", "ChromaLinkQuietZone", rootFrame)
  quietZone:SetPoint("TOPLEFT", rootFrame, "TOPLEFT", 0, 0)
  quietZone:SetLayer(config.requestedLayer - 1)
  ApplyColor(quietZone, config.GetPaletteColor(quietZoneConfig.backgroundSymbol or 0))
  return quietZone
end

local function ResolveDisplayCompensation(anchorWidth, anchorHeight, profile)
  local compensationConfig = config.displayCompensation or {}
  local baseScaleX = tonumber(profile.displayScaleX) or tonumber(profile.displayScale) or 1
  local baseScaleY = tonumber(profile.displayScaleY) or tonumber(profile.displayScale) or 1
  local compensationX = 1.0
  local compensationY = 1.0
  local reason = "disabled"

  if not compensationConfig.enabled then
    return {
      enabled = false,
      compensationX = 1.0,
      compensationY = 1.0,
      effectiveScaleX = baseScaleX,
      effectiveScaleY = baseScaleY,
      reason = reason,
      anchorRatioX = anchorWidth / math.max(1, profile.windowWidth),
      anchorRatioY = anchorHeight / math.max(1, profile.windowHeight)
    }
  end

  local anchorRatioX = anchorWidth / math.max(1, profile.windowWidth)
  local anchorRatioY = anchorHeight / math.max(1, profile.windowHeight)
  local allowShrink = compensationConfig.allowShrink and true or false

  if compensationConfig.mode == "anchor-ratio" then
    if anchorRatioX > 0 then
      compensationX = 1.0 / anchorRatioX
    end

    if anchorRatioY > 0 then
      compensationY = 1.0 / anchorRatioY
    end

    if not allowShrink then
      if compensationX < 1.0 then
        compensationX = 1.0
      end
      if compensationY < 1.0 then
        compensationY = 1.0
      end
    end

    reason = "anchor-ratio"
  end

  local minScaleX = allowShrink and 0.10 or 1.0
  local minScaleY = allowShrink and 0.10 or 1.0
  compensationX = math.max(minScaleX, math.min(compensationX, tonumber(compensationConfig.maxScaleX) or 4.0))
  compensationY = math.max(minScaleY, math.min(compensationY, tonumber(compensationConfig.maxScaleY) or 4.0))

  return {
    enabled = true,
    compensationX = compensationX,
    compensationY = compensationY,
    effectiveScaleX = baseScaleX * compensationX,
    effectiveScaleY = baseScaleY * compensationY,
    reason = reason,
    anchorRatioX = anchorRatioX,
    anchorRatioY = anchorRatioY
  }
end

local function ComputeLayout(renderState)
  local profile = renderState.profile
  local quietZoneConfig = config.quietZone or {}
  local observerConfig = config.observerLane or {}
  local anchorFrame = renderState.anchorFrame or UIParent or renderState.rootFrame
  local anchorWidth = ReadDimension(anchorFrame, "GetWidth", profile.windowWidth)
  local anchorHeight = ReadDimension(anchorFrame, "GetHeight", profile.windowHeight)
  local compensation = ResolveDisplayCompensation(anchorWidth, anchorHeight, profile)
  local effectiveScaleX = compensation.effectiveScaleX
  local effectiveScaleY = compensation.effectiveScaleY
  local bandWidth = profile.bandWidth * effectiveScaleX
  local bandHeight = profile.bandHeight * effectiveScaleY
  local rootWidth = bandWidth
  local totalBandHeight = ResolveTotalBandHeight(profile, effectiveScaleY)
  local rootHeight = totalBandHeight
  local requestedLeft = tonumber(config.stripOffsetX) or 0
  local requestedTop = tonumber(config.stripOffsetY) or 0

  if quietZoneConfig.enabled and quietZoneConfig.fullAnchorWidth then
    rootWidth = anchorWidth
    requestedLeft = 0
  end

  if quietZoneConfig.enabled then
    rootHeight = math.max(rootHeight, tonumber(quietZoneConfig.height) or totalBandHeight)
  end

  if renderState.observerLane ~= nil and renderState.observerLane.enabled then
    local probeBarHeight = tonumber(observerConfig.height) or 12
    local probeBarBottom = ResolveObserverLaneTop(profile) + probeBarHeight
    if probeBarBottom > rootHeight then
      rootHeight = probeBarBottom
    end
  end

  local maxLeft = math.max(0, anchorWidth - rootWidth)
  local maxTop = math.max(0, anchorHeight - rootHeight)
  local rootLeft = math.max(0, math.min(requestedLeft, maxLeft))
  local rootTop = math.max(0, math.min(requestedTop, maxTop))

  return {
    anchorFrame = anchorFrame,
    anchorWidth = anchorWidth,
    anchorHeight = anchorHeight,
    rootLeft = rootLeft,
    rootTop = rootTop,
    rootWidth = rootWidth,
    rootHeight = rootHeight,
    bandWidth = bandWidth,
    bandHeight = bandHeight,
    totalBandHeight = totalBandHeight,
    displayScaleX = effectiveScaleX,
    displayScaleY = effectiveScaleY,
    displayCompensation = compensation
  }
end

local function ApplyLayout(renderState)
  local profile = renderState.profile
  local observerConfig = config.observerLane or {}
  local layout = ComputeLayout(renderState)
  local displayScaleX = layout.displayScaleX
  local displayScaleY = layout.displayScaleY
  local index
  local bandWidth = layout.bandWidth
  local bandHeight = layout.bandHeight

  if renderState.lastRootLeft == layout.rootLeft
      and renderState.lastRootTop == layout.rootTop
      and renderState.lastRootWidth == layout.rootWidth
      and renderState.lastRootHeight == layout.rootHeight
      and renderState.lastAnchorWidth == layout.anchorWidth
      and renderState.lastAnchorHeight == layout.anchorHeight
      and renderState.lastDisplayScaleX == displayScaleX
      and renderState.lastDisplayScaleY == displayScaleY then
    return false
  end

  if renderState.rootFrame.ClearAllPoints ~= nil then
    renderState.rootFrame:ClearAllPoints()
  end
  renderState.rootFrame:SetPoint("TOPLEFT", layout.anchorFrame, "TOPLEFT", layout.rootLeft, layout.rootTop)
  renderState.rootFrame:SetWidth(layout.rootWidth)
  renderState.rootFrame:SetHeight(layout.rootHeight)

  if renderState.band.ClearAllPoints ~= nil then
    renderState.band:ClearAllPoints()
  end
  for index = 1, #renderState.bands do
    local band = renderState.bands[index]
    if band.ClearAllPoints ~= nil then
      band:ClearAllPoints()
    end
    band:SetPoint("TOPLEFT", renderState.rootFrame, "TOPLEFT", 0, (index - 1) * bandHeight)
    band:SetWidth(bandWidth)
    band:SetHeight(bandHeight)
  end

  if renderState.quietZone ~= nil then
    if renderState.quietZone.ClearAllPoints ~= nil then
      renderState.quietZone:ClearAllPoints()
    end
    renderState.quietZone:SetPoint("TOPLEFT", renderState.rootFrame, "TOPLEFT", 0, 0)
    renderState.quietZone:SetWidth(layout.rootWidth)
    renderState.quietZone:SetHeight(layout.rootHeight)
  end

  for index = 1, #renderState.bands do
    local segments = renderState.segmentRows[index]
    local band = renderState.bands[index]
    local segmentIndex
    for segmentIndex = 1, profile.segmentCount do
      local segment = segments[segmentIndex]
      if segment.ClearAllPoints ~= nil then
        segment:ClearAllPoints()
      end
      segment:SetPoint("TOPLEFT", band, "TOPLEFT", (segmentIndex - 1) * profile.segmentWidth * displayScaleX, 0)
      segment:SetWidth(profile.segmentWidth * displayScaleX)
      segment:SetHeight(profile.segmentHeight * displayScaleY)
    end
  end

  if renderState.observerLane ~= nil then
    local probeBarLeft = 0
    local probeBarTop = ResolveObserverLaneTop(profile)
    local probeBarWidth = bandWidth
    local probeBarHeight = observerConfig.height or 12
    local markerWidth = observerConfig.markerWidth or 20
    local markerHeight = probeBarHeight

    if renderState.observerLane.bar.ClearAllPoints ~= nil then
      renderState.observerLane.bar:ClearAllPoints()
    end
    renderState.observerLane.bar:SetPoint("TOPLEFT", renderState.rootFrame, "TOPLEFT", probeBarLeft, probeBarTop)
    renderState.observerLane.bar:SetWidth(probeBarWidth)
    renderState.observerLane.bar:SetHeight(probeBarHeight)
    renderState.observerLane.bar:SetVisible(renderState.observerLane.enabled)

    for index = 1, #renderState.observerLane.markers do
      local markerState = renderState.observerLane.markers[index]
      local marker = markerState.frame
      local x = markerState.fraction * math.max(0, probeBarWidth - markerWidth)

      if marker.ClearAllPoints ~= nil then
        marker:ClearAllPoints()
      end
      marker:SetPoint("TOPLEFT", renderState.observerLane.bar, "TOPLEFT", x, 0)
      marker:SetWidth(markerWidth)
      marker:SetHeight(markerHeight)
      marker:SetVisible(renderState.observerLane.enabled)
    end
  end

  renderState.lastRootLeft = layout.rootLeft
  renderState.lastRootTop = layout.rootTop
  renderState.lastRootWidth = layout.rootWidth
  renderState.lastRootHeight = layout.rootHeight
  renderState.lastAnchorWidth = layout.anchorWidth
  renderState.lastAnchorHeight = layout.anchorHeight
  renderState.lastDisplayScaleX = displayScaleX
  renderState.lastDisplayScaleY = displayScaleY
  renderState.lastDisplayCompensation = layout.displayCompensation
  return true
end

function ChromaLink.Render.Initialize(rootFrame, anchorFrame)
  local profile = config.profile
  local defaultScaleX = tonumber(profile.displayScaleX) or tonumber(profile.displayScale) or 1
  local defaultScaleY = tonumber(profile.displayScaleY) or tonumber(profile.displayScale) or 1
  local quietZone = CreateQuietZone(rootFrame)
  local observerLane = CreateObserverLane(rootFrame, profile)
  local bands = {}
  local segmentRows = {}
  local lastSymbolRows = {}
  local stripCount = ResolveStripCount(profile)
  local bandIndex
  local index

  for bandIndex = 1, stripCount do
    local bandNameSuffix = (bandIndex == 1) and "" or tostring(bandIndex)
    local band = UI.CreateFrame("Frame", "ChromaLinkBand" .. bandNameSuffix, rootFrame)
    local segments = {}
    local lastSymbols = {}

    band:SetPoint("TOPLEFT", rootFrame, "TOPLEFT", 0, (bandIndex - 1) * profile.bandHeight * defaultScaleY)
    band:SetWidth(profile.bandWidth * defaultScaleX)
    band:SetHeight(profile.bandHeight * defaultScaleY)
    band:SetLayer(config.requestedLayer)
    ApplyColor(band, config.GetPaletteColor(0))

    for index = 1, profile.segmentCount do
      local segmentName = string.format("ChromaLinkSegment%s_%d", bandNameSuffix, index)
      local segment = UI.CreateFrame("Frame", segmentName, band)
      segment:SetPoint(
        "TOPLEFT",
        band,
        "TOPLEFT",
        (index - 1) * profile.segmentWidth * defaultScaleX,
        0)
      segment:SetWidth(profile.segmentWidth * defaultScaleX)
      segment:SetHeight(profile.segmentHeight * defaultScaleY)
      segment:SetLayer(config.requestedLayer + 1)
      ApplyColor(segment, config.GetPaletteColor(0))
      segments[index] = segment
      lastSymbols[index] = -1
    end

    bands[bandIndex] = band
    segmentRows[bandIndex] = segments
    lastSymbolRows[bandIndex] = lastSymbols
  end

  return {
    profile = profile,
    rootFrame = rootFrame,
    anchorFrame = anchorFrame,
    quietZone = quietZone,
    band = bands[1],
    bands = bands,
    observerLane = observerLane,
    segments = segmentRows[1],
    segmentRows = segmentRows,
    lastSymbols = lastSymbolRows[1],
    lastSymbolRows = lastSymbolRows,
    lastRootLeft = nil,
    lastRootTop = nil,
    lastRootWidth = nil,
    lastRootHeight = nil,
    lastAnchorWidth = nil,
    lastAnchorHeight = nil,
    lastDisplayScaleX = nil,
    lastDisplayScaleY = nil,
    lastDisplayCompensation = nil
  }
end

function ChromaLink.Render.SyncLayout(renderState)
  return ApplyLayout(renderState)
end

function ChromaLink.Render.SetObserverEnabled(renderState, enabled)
  if renderState == nil or renderState.observerLane == nil then
    return false
  end

  renderState.observerLane.enabled = enabled and true or false
  return ApplyLayout(renderState)
end

function ChromaLink.Render.GetDisplayCompensationSummary(renderState)
  if renderState == nil then
    return nil
  end

  local summary = renderState.lastDisplayCompensation
  if summary == nil then
    return nil
  end

  return summary
end

function ChromaLink.Render.Update(renderState, symbols)
  local changed = false
  local symbolRows
  local rowIndex
  local index

  if ApplyLayout(renderState) then
    changed = true
  end

  if type(symbols[1]) == "table" then
    symbolRows = symbols
  else
    symbolRows = { symbols }
  end

  for rowIndex = 1, #renderState.bands do
    local rowSymbols = symbolRows[rowIndex] or {}
    local segments = renderState.segmentRows[rowIndex]
    local lastSymbols = renderState.lastSymbolRows[rowIndex]

    for index = 1, renderState.profile.segmentCount do
      local symbol = rowSymbols[index] or 0
      if lastSymbols[index] ~= symbol then
        ApplyColor(segments[index], config.GetPaletteColor(symbol))
        lastSymbols[index] = symbol
        changed = true
      end
    end
  end

  return changed
end
