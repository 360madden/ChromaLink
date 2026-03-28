ChromaLink = ChromaLink or {}
ChromaLink.Render = {}

local config = ChromaLink.Config

local function ApplyColor(frame, color)
  frame:SetBackgroundColor(color[1], color[2], color[3], color[4])
end

function ChromaLink.Render.Initialize(rootFrame)
  local profile = config.profile
  local band = UI.CreateFrame("Frame", "ChromaLinkBand", rootFrame)
  local segments = {}
  local lastSymbols = {}
  local index

  band:SetPoint("TOPLEFT", rootFrame, "TOPLEFT", 0, 0)
  band:SetWidth(profile.bandWidth)
  band:SetHeight(profile.bandHeight)
  band:SetLayer(config.requestedLayer)
  ApplyColor(band, config.GetPaletteColor(0))

  for index = 1, profile.segmentCount do
    local segment = UI.CreateFrame("Frame", "ChromaLinkSegment" .. tostring(index), band)
    segment:SetPoint("TOPLEFT", band, "TOPLEFT", (index - 1) * profile.segmentWidth, 0)
    segment:SetWidth(profile.segmentWidth)
    segment:SetHeight(profile.segmentHeight)
    segment:SetLayer(config.requestedLayer + 1)
    ApplyColor(segment, config.GetPaletteColor(0))
    segments[index] = segment
    lastSymbols[index] = -1
  end

  return {
    profile = profile,
    band = band,
    segments = segments,
    lastSymbols = lastSymbols
  }
end

function ChromaLink.Render.Update(renderState, symbols)
  local changed = false
  local index

  for index = 1, renderState.profile.segmentCount do
    local symbol = symbols[index] or 0
    if renderState.lastSymbols[index] ~= symbol then
      ApplyColor(renderState.segments[index], config.GetPaletteColor(symbol))
      renderState.lastSymbols[index] = symbol
      changed = true
    end
  end

  return changed
end
