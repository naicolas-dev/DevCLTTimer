# Dev CLT Timer

Cronômetro de jornada de trabalho para devs CLT. Controle de trabalho, pausa, hora extra e relatórios semanais/mensais.

## Stack

- **C# / .NET 8 + WPF (MVVM)**
- **SQLite** para persistência local (`%APPDATA%\DevCLTTimer\devclt.db`)
- **Toast Notifications** (Windows 10+)
- **System Tray** com menu dinâmico

## Arquitetura

```
src/
├── DevCLT.Core/            # Motor de estado, modelos, interfaces (net8.0, cross-platform)
├── DevCLT.Infrastructure/  # SQLite repository, queries de relatório (net8.0)
└── DevCLT.WindowsApp/      # WPF, Views/ViewModels, Tray, Toast (net8.0-windows10.0.17763.0)
tests/
└── DevCLT.Tests/           # Testes unitários (xUnit + Moq)
```

## Como rodar (desenvolvimento)

```bash
dotnet build DevCLTTimer.sln
dotnet run --project src/DevCLT.WindowsApp/DevCLT.WindowsApp.csproj
```

Ou abra `DevCLTTimer.sln` no Visual Studio / Rider e pressione F5.

## Como publicar (.exe)

```bash
dotnet publish src/DevCLT.WindowsApp/DevCLT.WindowsApp.csproj -c Release
```

O executável single-file self-contained será gerado em:
```
src/DevCLT.WindowsApp/bin/Release/net8.0-windows10.0.17763.0/win-x64/publish/DevCLTTimer.exe
```

## Funcionalidades MVP

- ✅ Tela de Setup: horas trabalho, pausa, intervalo notificação hora extra
- ✅ Timer Working: countdown + botão "Iniciar pausa" + "Encerrar jornada" (confirm)
- ✅ Timer Break: countdown; ao acabar → toast + modal com "Retomar trabalho"
- ✅ Jornada concluída: modal com "Concluir" / "Iniciar hora extra"
- ✅ Hora extra: conta pra cima, notifica a cada X min configurável
- ✅ Persistência SQLite: sessions, segments (work/break/overtime)
- ✅ Histórico: visão Semana/Mês (ISO week seg-dom)
- ✅ Recovery: card ao reabrir se sessão ativa ("Retomar" / "Descartar")
- ✅ Empty state no histórico
- ✅ System Tray: menu dinâmico baseado no estado
- ✅ Close → minimize to tray com jornada ativa
- ✅ Design system: Light theme (Argila/Sálvia), tokens, componentes WPF
- ✅ TimerRing circular (2px) com warning color

## V1

- [x] Dark theme (Floresta Noturna)
- [x] Title Bar Personalizada
- [x] Satoshi font embarcada
- [ ] Tray tooltip e Menu de Contexto com tempo restante
- [ ] Painel anual
- [ ] Hotkeys
- [ ] Export CSV

## V2

- [ ] Port para Linux (Avalonia)