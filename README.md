# StreamChat AI for LiveSplit

A [LiveSplit](https://livesplit.org) component that lets your
[StreamChat AI](https://www.streamchatai.com) chat bot react to your run: a cheer on
a gold split, a celebration on a new personal best, something kind when you reset.
Your bot can also tell chat where you are in the run (`!splits`), how the session
is going (`!attempts`), and, if you switch it on, keep a log of your attempts.

It draws nothing on your layout. It only listens to the timer.

## Install

1. Download `StreamChatAI.LiveSplit.dll` from the
   [latest release](https://github.com/StreamChat-AI/livesplit-component/releases/latest).
2. Close LiveSplit, and put the file in the `Components` folder inside your LiveSplit folder.
3. Open LiveSplit, right-click it, choose **Edit Layout**, press **+** and add
   **Other > StreamChat AI**.
4. Open **Layout Settings**, go to the **StreamChat AI** tab and press **Connect**.
   LiveSplit shows a code like `ABCD-2345`.
5. Go to [streamchatai.com/b/livesplit](https://www.streamchatai.com/b/livesplit)
   and type the code. Keep the settings window off stream while you do: anyone who
   sees the code could type it first. The tab shows which account you connected to,
   so you would notice.
6. Choose what chat hears on the **Reactions** page of the website.

## What it sends

Only timer events, to `https://api.streamchatai.com/livesplit/events`:

| Event | When | What is included |
| --- | --- | --- |
| `start` | You start the timer | Game, category, attempt count, the first split's name, PB time |
| `split` | You split | The split's name, time, segment time, time against your PB, and whether it was a gold |
| `finish` | Your last split | The final time, your previous PB and whether this beat it |
| `skip` / `undo` | You skip or undo a split | Which split |
| `reset` | You reset | Which split you were on and how long the run lasted |
| `pause` / `resume` | You pause or resume | Nothing extra |

Nothing about your computer is sent, apart from its name once, while connecting,
so the website can label the connection ("Connected: RUNNING-PC").

Untick **Send my run to StreamChat AI** in the tab to stop sending without
disconnecting.

## Your token

Connecting gives this copy of LiveSplit a token. It is **not** stored in your
layout file, because runners share layouts and a token in there would let anyone
with the file post to your chat. It is kept in
`%AppData%\StreamChatAI\livesplit-connection.bin`, encrypted so only your Windows
account can read it.

To revoke it, press **Disconnect** in the tab, or **Disconnect** beside it on the
website.

## Building

The component targets .NET Framework 4.8.1, the same as LiveSplit, and builds on
Windows or Linux.

```bash
scripts/fetch-livesplit.sh             # downloads the LiveSplit assemblies it compiles against
dotnet build src/StreamChatAI.LiveSplit -c Release
dotnet test tests                      # the payload, JSON and sending logic, no LiveSplit needed
```

On Linux without the SDK installed:

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet build src/StreamChatAI.LiveSplit -c Release
```

To point a development copy at a local API, set the `STREAMCHATAI_API_URL`
environment variable before starting LiveSplit. It is deliberately not a layout
setting: layouts get shared, and a layout that could change the address could
collect your token.

Everything that does not touch LiveSplit or Windows lives in `src/Core` and is
compiled into both the component and the tests, so the component stays a single
DLL with no dependencies to install beside it.

## Releasing

1. Bump `Version`, `AssemblyVersion` and `FileVersion` in
   `src/StreamChatAI.LiveSplit/StreamChatAI.LiveSplit.csproj`.
2. Add an `<update>` entry at the top of `update.StreamChatAI.xml`, and copy the
   built DLL to `Components/StreamChatAI.LiveSplit.dll`. LiveSplit's own updater
   reads both from this repository's `main` branch.
3. Tag `vX.Y.Z` and push. CI runs the tests and attaches the DLL to a GitHub release.

## Licence

MIT. See [LICENSE](LICENSE).
