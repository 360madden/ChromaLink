ChromaLink = ChromaLink or {}
ChromaLink.ErrorTrap = {
  lastErrorId = nil
}

function ChromaLink.ErrorTrap.OnSystemError(_, errorData)
  if errorData == nil or errorData.addon ~= ChromaLink.Config.addonIdentifier then
    return
  end

  if errorData.id ~= nil and errorData.id == ChromaLink.ErrorTrap.lastErrorId then
    return
  end

  ChromaLink.ErrorTrap.lastErrorId = errorData.id
  ChromaLink.Diagnostics.Log("Addon error: " .. tostring(errorData.message))
end

Command.Event.Attach(
  Event.System.Error,
  ChromaLink.ErrorTrap.OnSystemError,
  "ChromaLink.ErrorTrap.OnSystemError"
)
