ChromaLink = ChromaLink or {}
ChromaLink.ErrorTrap = {
  lastErrorId = nil
}

local function EmitError(message)
  local formatted = "[ChromaLink] " .. tostring(message)

  if Command ~= nil and Command.Console ~= nil and Command.Console.Display ~= nil then
    Command.Console.Display("general", true, "<font color=\"#FF5A5A\">" .. formatted .. "</font>", true)
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

  EmitError(
    "error"
      .. " | type=" .. tostring(errorData.type or "unknown")
      .. " | file=" .. tostring(errorData.file or "n/a")
      .. " | message=" .. tostring(errorData.error or "n/a")
  )
end

Command.Event.Attach(
  Event.System.Error,
  ChromaLink.ErrorTrap.OnSystemError,
  "ChromaLink.ErrorTrap.OnSystemError"
)
