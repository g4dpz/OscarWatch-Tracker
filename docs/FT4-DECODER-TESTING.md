# FT4 decoder testing with real recordings

The FT4 decoder's thresholds (AP agreement, SNR floor, DT limits, drift search) were first tuned on synthetic signals. This guide covers recording real satellite passes and turning them into regression tests, so a decoder change can be measured on real RS-44 audio instead of guessed at.

## 1. Record a pass

1. Open **Tools, FT4**, then **Settings, Modem**.
2. Under **Decoder testing**, tick **Save receive slot audio**.
3. Leave the FT4 window open. Receive slots are saved while the tracked satellite is at −2° elevation or higher, so each pass also gets a few noise-only slots around AOS and LOS for false-decode tests. Transmit slots, and the time between passes, are not saved.
4. **Open folder** shows the recordings, in `%APPDATA%\OscarWatch\ft4-slots\` on Windows.

Each slot produces two files with the same name, for example:

```text
RS-44_20261007_214952.5.wav    full 7.5 s capture, 12 kHz mono 16-bit
RS-44_20261007_214952.5.json   every decode pass on that slot
```

A slot is about 180 kB, so a 15-minute pass is roughly 10 to 20 MB depending on how much you transmit. Files older than 14 days are deleted automatically. Turn the setting off when you have what you need.

## 2. What the JSON holds

The JSON is written one slot after the WAV, once the end-of-slot passes have finished. It records the inputs of each native decode call, so the test can replay that call exactly:

| Field | Meaning |
|----|----|
| `slotUtc`, `satellite`, `elevationDeg` | When and where the slot was heard |
| `passes[].sampleCount` | Audio the pass used. The early decode stops near 6 s (about 72000 samples); end-of-slot passes use the whole slot |
| `passes[].dopplerSlopeHzPerSec` | Downlink slope removed before decoding. 0 means the raw capture |
| `passes[].deep` | Deep decode (below 20° elevation) |
| `passes[].fMinHz`, `fMaxHz` | Search band |
| `passes[].apHints`, `apHz` | AP messages tried and the centre frequency. Absent when AP was not active |
| `passes[].driftMaxHzPerSec`, `driftSteps` | Leftover drift search |
| `passes[].decodes[]` | What the decoder returned: text, Hz, DT, SNR, `ap` (0 CRC, 1 guessed, 2 hint that passed the CRC) and drift |

`decodes` is the raw decoder output, before the app's own filters (DT limits, the AP guards, duplicate removal). A line can therefore appear here that never reached the decode list.

Below 5° elevation a slot normally has three passes: the early decode, the end-of-slot decode on the Doppler-corrected audio, and the same on the raw audio.

## 3. Find the interesting slots

Worth keeping:

- **False decodes.** An AP line that was wrong, or any message nobody sent. Today's example was `MM9SQL KK7MNC R+07`, read from KK7MNC's real `KJ4SKO KK7MNC +00`.
- **Misses.** A signal plainly visible on the waterfall, or decoded by another station (PSK Reporter, a second receiver, WSJT-X on the same audio), that OscarWatch did not decode.
- **Weak successes.** Decodes near the limit (−15 dB and below) that a change must not lose.
- **Low-elevation and fast-Doppler slots,** where the drift search and the raw-audio pass matter.

The slot time in the decode list (UTC) matches the file name. The app log (`%APPDATA%\OscarWatch\logs`) uses local time.

To find every AP line in a day's recordings in PowerShell:

```powershell
Select-String -Path "$env:APPDATA\OscarWatch\ft4-slots\*.json" -Pattern '"ap": [12]' -List
```

## 4. Add a recording to the test corpus

1. Copy the WAV and its JSON into `OscarWatch.Tests/Ft4/Corpus/`.
2. Add an `expect` block at the top level of the JSON:

```json
"expect": {
  "decode": ["KJ4SKO KK7MNC +00", "CQ KD2YIB FN22"],
  "reject": ["MM9SQL KK7MNC R+07"],
  "note": "False AP reply from KK7MNC's report to KJ4SKO, 7 Oct, 0° elevation"
}
```

- `decode`: messages at least one pass must produce.
- `reject`: messages no pass may produce.
- `note`: free text, for the next person.

A recording with no `expect` block is still replayed and printed, but not judged.

Only list messages you are sure of. A `decode` entry should be a message that was really sent. A `reject` entry should be a message that certainly was not sent.

## 5. Run the tests

```powershell
dotnet test OscarWatch.Tests --filter "FullyQualifiedName~Ft4SlotCorpusTests" --logger "console;verbosity=detailed"
```

The detailed log prints what every recording decodes now, then lists each failure as `missed <message>` or `produced rejected <message>`.

To replay a whole folder without copying anything, for example an afternoon's recordings straight from the app:

```powershell
$env:OSCARWATCH_FT4_CORPUS = "$env:APPDATA\OscarWatch\ft4-slots"
dotnet test OscarWatch.Tests --filter "FullyQualifiedName~Ft4SlotCorpusTests" --logger "console;verbosity=detailed"
Remove-Item Env:OSCARWATCH_FT4_CORPUS
```

## 6. Measure a decoder change

1. Run the corpus on the current build and keep the output.
2. Change the decoder (for example `kApQualityMin` or `kApPrefilterMin` in `native/oscarwatch_ft8/oscarwatch_ft8.c`) and rebuild the native library as described in `native/oscarwatch_ft8/README.md`.
3. Run the corpus again and compare: no new `missed` lines, no new `produced rejected` lines, and ideally a few more real decodes across the unjudged recordings.

The tests replay the recorded passes through the native decoder only. Filters in `Ft4ModemService` (DT limits, the SNR floor for guessed hints, one AP line per station per slot, undoing a contradicted AP reply) are not part of the replay, and are covered by their own unit tests.

## Notes

- The WAV is 16-bit. A signal right on the decoding limit can replay very slightly differently from the live decode. Keep expectations to messages with some margin, or note the borderline ones.
- The decoder keeps a table of callsigns it has heard so it can show hashed calls. A replay starts with whatever earlier tests left in it, so a slot whose message relies on a hashed call may print `<...>` instead of the call.
- The synthetic sensitivity and drift sweeps are in `Ft4DecodeSweepTests` and run with `OSCARWATCH_FT4_SWEEP=1`.
