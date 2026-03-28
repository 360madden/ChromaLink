-- script name: RIFT/ErrorTrap.lua
-- version: 0.1.0
-- purpose: Reports ChromaLink-specific RIFT addon load/runtime errors to chat as early as possible.
-- dependencies: Core/Config.lua
-- important assumptions: Event.System.Error is available during addon loading and reports fileLoad/fileRun details for this addon.
-- protocol version: ChromaLink
-- framework module role: RIFT diagnostics bootstrap
-- character count note: Character count not precomputed; measure with tooling if needed.

ChromaLink = ChromaLink or {}
ChromaLink.ErrorTrap = {
  lastErrorId = nil
}

local function EmitErrorLine(message)
  local formatted = "[ChromaLink] " .. tostring(message)

  if Command ~= nil and Command.Console ~= nil and Command.Console.Display ~= nil then
    local htmlText = "<font color=\"#FF5A5A\">" .. formatted .. "</font>"
    Command.Console.Display("general", true, htmlText, true)
    return
  end

  print(formatted)
end

function ChromaLink.ErrorTrap.OnSystemError(_, errorData)
  if type(errorData) ~= "table" then
    return
  end

  if errorData.addon ~= ChromaLink.Config.addonIdentifier then
    return
  end

  if errorData.id ~= nil and errorData.id == ChromaLink.ErrorTrap.lastErrorId then
    return
  end

  ChromaLink.ErrorTrap.lastErrorId = errorData.id

  local parts = {
    "error",
    "type=" .. tostring(errorData.type or "unknown"),
    "file=" .. tostring(errorData.file or "n/a"),
    "message=" .. tostring(errorData.error or "n/a")
  }

  if errorData.info ~= nil then
    parts[#parts + 1] = "info=" .. tostring(errorData.info)
  end

  EmitErrorLine(table.concat(parts, " | "))
end

Command.Event.Attach(
  Event.System.Error,
  ChromaLink.ErrorTrap.OnSystemError,
  "ChromaLink.ErrorTrap.OnSystemError"
)

-- end-of-script marker comment
