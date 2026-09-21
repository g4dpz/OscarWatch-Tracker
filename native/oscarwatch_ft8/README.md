# oscarwatch_ft8

Thin C wrapper around [ft8_lib](https://github.com/kgoba/ft8_lib) (MIT) for OscarWatch FT4 encode/decode.

## Output locations

| Platform | RID | File |
|----------|-----|------|
| Windows x64 | `win-x64` | `OscarWatch/runtimes/win-x64/native/oscarwatch_ft8.dll` |
| Windows ARM64 | `win-arm64` | `OscarWatch/runtimes/win-arm64/native/oscarwatch_ft8.dll` |
| macOS Apple Silicon | `osx-arm64` | `OscarWatch/runtimes/osx-arm64/native/oscarwatch_ft8.dylib` |
| macOS Intel | `osx-x64` | `OscarWatch/runtimes/osx-x64/native/oscarwatch_ft8.dylib` |
| Linux x64 | `linux-x64` | `OscarWatch/runtimes/linux-x64/native/oscarwatch_ft8.so` |
| Linux ARM64 | `linux-arm64` | `OscarWatch/runtimes/linux-arm64/native/oscarwatch_ft8.so` |

The managed loader (`Ft8Native`) looks under `runtimes/<rid>/native/` for the matching file name.

## Build (this machine)

From the repo root (needs CMake and a C toolchain):

```bash
# Example: macOS Apple Silicon
cmake -S native/oscarwatch_ft8 -B native/oscarwatch_ft8/build-osx-arm64
cmake --build native/oscarwatch_ft8/build-osx-arm64 --config Release
mkdir -p OscarWatch/runtimes/osx-arm64/native
cp native/oscarwatch_ft8/build-osx-arm64/oscarwatch_ft8.dylib \
  OscarWatch/runtimes/osx-arm64/native/

# macOS Intel (cross from Apple Silicon)
cmake -S native/oscarwatch_ft8 -B native/oscarwatch_ft8/build-osx-x64 \
  -DCMAKE_OSX_ARCHITECTURES=x86_64
cmake --build native/oscarwatch_ft8/build-osx-x64 --config Release
mkdir -p OscarWatch/runtimes/osx-x64/native
cp native/oscarwatch_ft8/build-osx-x64/oscarwatch_ft8.dylib \
  OscarWatch/runtimes/osx-x64/native/
```

Publish CI builds the library for each RID before `dotnet publish` so release packages include the native binary.
