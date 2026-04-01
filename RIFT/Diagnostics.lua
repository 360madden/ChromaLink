ChromaLink = ChromaLink or {}
ChromaLink.Diagnostics = {}

local layoutTraceState = {}

function ChromaLink.Diagnostics.Log(message)
  local formatted = "[ChromaLink] " .. tostring(message)

  if Command ~= nil and Command.Console ~= nil and Command.Console.Display ~= nil then
    Command.Console.Display("general", true, "<font color=\"#64D2FF\">" .. formatted .. "</font>", true)
    return
  end

  print(formatted)
end

local function SafeCall(frame, methodName)
  if frame == nil or frame[methodName] == nil then
    return false, nil
  end

  return pcall(function()
    return frame[methodName](frame)
  end)
end

local function GetFrameName(frame)
  local ok, value = SafeCall(frame, "GetName")
  if ok and value ~= nil then
    return tostring(value)
  end

  return tostring(frame)
end

local function SummarizeAxis(axis)
  if axis == nil then
    return "-"
  end

  local parts = {}
  if axis.size ~= nil then
    table.insert(parts, string.format("size=%.2f", tonumber(axis.size) or 0))
  else
    table.insert(parts, "size=-")
  end

  local positions = {}
  local key
  for key in pairs(axis) do
    if type(key) == "number" then
      table.insert(positions, key)
    end
  end
  table.sort(positions)

  local anchors = {}
  for _, position in ipairs(positions) do
    local entry = axis[position]
    local layoutName = "nil"
    if entry ~= nil and entry.layout ~= nil then
      layoutName = GetFrameName(entry.layout)
    end

    table.insert(anchors, string.format(
      "%.2f->%s@%.2f%+.1f",
      tonumber(position) or 0,
      layoutName,
      tonumber(entry.position) or 0,
      tonumber(entry.offset) or 0))
  end

  if #anchors > 0 then
    table.insert(parts, "[" .. table.concat(anchors, ", ") .. "]")
  end

  return table.concat(parts, " ")
end

function ChromaLink.Diagnostics.DescribeLayout(frame)
  if frame == nil then
    return "frame=nil"
  end

  local left, top, right, bottom = 0, 0, 0, 0
  local okBounds = pcall(function()
    left, top, right, bottom = frame:GetBounds()
  end)

  local widthOk, width = SafeCall(frame, "GetWidth")
  local heightOk, height = SafeCall(frame, "GetHeight")
  local visibleOk, visible = SafeCall(frame, "GetVisible")
  local layerOk, layer = SafeCall(frame, "GetLayer")
  local strataOk, strata = SafeCall(frame, "GetStrata")
  local readAllOk, readAll = SafeCall(frame, "ReadAll")

  local boundsText
  if okBounds then
    boundsText = string.format("bounds=(%.1f,%.1f)-(%.1f,%.1f)", left, top, right, bottom)
  else
    boundsText = "bounds=(error)"
  end

  local xLayout = "-"
  local yLayout = "-"
  if readAllOk and readAll ~= nil then
    xLayout = SummarizeAxis(readAll.x)
    yLayout = SummarizeAxis(readAll.y)
  end

  return string.format(
    "%s size=%.1fx%.1f visible=%s layer=%s strata=%s x={%s} y={%s}",
    boundsText,
    widthOk and (tonumber(width) or 0) or -1,
    heightOk and (tonumber(height) or 0) or -1,
    visibleOk and tostring(visible) or "?",
    layerOk and tostring(layer) or "?",
    strataOk and tostring(strata) or "?",
    xLayout,
    yLayout)
end

function ChromaLink.Diagnostics.LogLayout(label, frame, reason)
  ChromaLink.Diagnostics.Log(string.format(
    "%s%s %s",
    tostring(label),
    reason ~= nil and (" [" .. tostring(reason) .. "]") or "",
    ChromaLink.Diagnostics.DescribeLayout(frame)))
end

function ChromaLink.Diagnostics.AttachLayoutTrace(frame, label, maxTraceEvents)
  if frame == nil or frame.EventAttach == nil then
    return
  end

  local traceLabel = tostring(label)
  local config = ChromaLink.Config.layoutDiagnostics or {}
  local trace = layoutTraceState[traceLabel]
  if trace ~= nil then
    return
  end

  trace = {
    emitted = 0,
    limit = tonumber(maxTraceEvents) or tonumber(config.maxTraceEventsPerFrame) or 12,
    lastSummary = nil
  }
  layoutTraceState[traceLabel] = trace

  local function Emit(reason)
    local summary = ChromaLink.Diagnostics.DescribeLayout(frame)
    if summary == trace.lastSummary and trace.emitted > 0 then
      return
    end

    trace.lastSummary = summary
    trace.emitted = trace.emitted + 1

    if trace.emitted <= trace.limit then
      ChromaLink.Diagnostics.Log(string.format("%s [%s] %s", traceLabel, tostring(reason), summary))
      return
    end

    if trace.emitted == (trace.limit + 1) then
      ChromaLink.Diagnostics.Log(string.format("%s trace limit reached; suppressing more layout spam.", traceLabel))
    end
  end

  frame:EventAttach(Event.UI.Layout.Move, function()
    Emit("move")
  end, traceLabel .. ".Move")

  frame:EventAttach(Event.UI.Layout.Size, function()
    Emit("size")
  end, traceLabel .. ".Size")

  Emit("initial")
end
