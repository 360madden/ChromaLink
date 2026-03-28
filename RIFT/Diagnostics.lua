ChromaLink = ChromaLink or {}
ChromaLink.Diagnostics = {}

function ChromaLink.Diagnostics.Log(message)
  local formatted = "[ChromaLink] " .. tostring(message)

  if Command ~= nil and Command.Console ~= nil and Command.Console.Display ~= nil then
    Command.Console.Display("general", true, "<font color=\"#64D2FF\">" .. formatted .. "</font>", true)
    return
  end

  print(formatted)
end
