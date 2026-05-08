#!/usr/bin/env bash
set -euo pipefail

# Default values
CONFIGURATION="Release"
RUNTIME=""
SELF_CONTAINED=false
NO_SINGLE_FILE=false
READY_TO_RUN=false
TRIM=false
OUTPUT_DIR=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -c|--configuration)
            CONFIGURATION="$2"
            if [[ "$CONFIGURATION" != "Debug" && "$CONFIGURATION" != "Release" ]]; then
                echo "Error: Invalid configuration '$CONFIGURATION'. Must be 'Debug' or 'Release'." >&2
                exit 1
            fi
            shift 2
            ;;
        -r|--runtime)
            RUNTIME="$2"
            if [[ -n "$RUNTIME" && "$RUNTIME" != "win-x64" && "$RUNTIME" != "win-arm64" && "$RUNTIME" != "linux-x64" && "$RUNTIME" != "linux-arm64" && "$RUNTIME" != "osx-x64" && "$RUNTIME" != "osx-arm64" ]]; then
                echo "Error: Invalid runtime '$RUNTIME'." >&2
                exit 1
            fi
            shift 2
            ;;
        --self-contained)
            SELF_CONTAINED=true
            shift
            ;;
        --no-single-file)
            NO_SINGLE_FILE=true
            shift
            ;;
        --ready-to-run)
            READY_TO_RUN=true
            shift
            ;;
        --trim)
            TRIM=true
            shift
            ;;
        -o|--output)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  -c, --configuration <Debug|Release>   Build configuration (default: Release)"
            echo "  -r, --runtime <rid>                   Target runtime identifier"
            echo "  --self-contained                      Publish self-contained"
            echo "  --no-single-file                      Disable single-file publishing"
            echo "  --ready-to-run                        Enable ReadyToRun"
            echo "  --trim                                Enable trimming"
            echo "  -o, --output <dir>                    Output directory"
            echo ""
            echo "Valid runtime identifiers:"
            echo "  win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64"
            exit 0
            ;;
        *)
            echo "Unknown option: $1" >&2
            exit 1
            ;;
    esac
done

# Determine script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Resolve output directory
if [[ -z "$OUTPUT_DIR" ]]; then
    if [[ -n "$RUNTIME" ]]; then
        OUTPUT_DIR="$ROOT_DIR/publish/$RUNTIME"
    else
        OUTPUT_DIR="$ROOT_DIR/publish"
    fi
else
    # If relative path, resolve against root dir
    case "$OUTPUT_DIR" in
        /*) ;; # already absolute
        *) OUTPUT_DIR="$ROOT_DIR/$OUTPUT_DIR" ;;
    esac
fi

PROJECT_PATH="$ROOT_DIR/CharacterKiller.CLI/CharacterKiller.CLI.csproj"

if [[ ! -f "$PROJECT_PATH" ]]; then
    echo "Error: Project file not found: $PROJECT_PATH" >&2
    exit 1
fi

if [[ -d "$OUTPUT_DIR" ]]; then
    echo "Cleaning old output: $OUTPUT_DIR"
    rm -rf "$OUTPUT_DIR"
fi

# Build dotnet publish arguments
PUBLISH_ARGS=(
    "publish"
    "$PROJECT_PATH"
    "-c" "$CONFIGURATION"
    "--output" "$OUTPUT_DIR"
)

if [[ -n "$RUNTIME" ]]; then
    PUBLISH_ARGS+=("--runtime" "$RUNTIME")
fi

if [[ "$SELF_CONTAINED" == true ]]; then
    PUBLISH_ARGS+=("--self-contained")
else
    PUBLISH_ARGS+=("--no-self-contained")
fi

if [[ "$NO_SINGLE_FILE" == false ]]; then
    PUBLISH_ARGS+=("/p:PublishSingleFile=true")
fi

if [[ "$READY_TO_RUN" == true ]]; then
    PUBLISH_ARGS+=("/p:PublishReadyToRun=true")
fi

if [[ "$TRIM" == true ]]; then
    PUBLISH_ARGS+=("/p:PublishTrimmed=true")
fi

if [[ "$CONFIGURATION" == "Release" ]]; then
    PUBLISH_ARGS+=("/p:DebugType=None")
    PUBLISH_ARGS+=("/p:DebugSymbols=false")
fi

# Determine display values
if [[ -n "$RUNTIME" ]]; then
    RUNTIME_DISPLAY="$RUNTIME"
else
    RUNTIME_DISPLAY="Current platform (framework-dependent)"
fi

if [[ "$SELF_CONTAINED" == true ]]; then
    SELF_CONTAINED_DISPLAY="Yes"
else
    SELF_CONTAINED_DISPLAY="No"
fi

if [[ "$NO_SINGLE_FILE" == false ]]; then
    SINGLE_FILE_DISPLAY="Yes"
else
    SINGLE_FILE_DISPLAY="No"
fi

if [[ "$READY_TO_RUN" == true ]]; then
    READY_TO_RUN_DISPLAY="Yes"
else
    READY_TO_RUN_DISPLAY="No"
fi

if [[ "$TRIM" == true ]]; then
    TRIM_DISPLAY="Yes"
else
    TRIM_DISPLAY="No"
fi

echo "========================================"
echo "Building CharacterKiller.CLI"
echo "========================================"
echo "Project  : $PROJECT_PATH"
echo "Config   : $CONFIGURATION"
echo "Runtime  : $RUNTIME_DISPLAY"
echo "Self-contained : $SELF_CONTAINED_DISPLAY"
echo "Single file    : $SINGLE_FILE_DISPLAY"
echo "ReadyToRun     : $READY_TO_RUN_DISPLAY"
echo "Trim           : $TRIM_DISPLAY"
echo "Output   : $OUTPUT_DIR"
echo "========================================"

START_TIME=$(date +%s)
dotnet "${PUBLISH_ARGS[@]}"

END_TIME=$(date +%s)
ELAPSED=$((END_TIME - START_TIME))

echo ""
printf "Build succeeded in %d.%02ds\n" $((ELAPSED / 1)) $((ELAPSED % 100))
echo ""
echo "Artifacts:"

if [[ -d "$OUTPUT_DIR" ]]; then
    EXE_NAME="CharacterKiller.CLI"
    if [[ "$RUNTIME" == win* ]]; then
        EXE_NAME="${EXE_NAME}.exe"
    fi

    for f in "$OUTPUT_DIR"/*; do
        if [[ -e "$f" ]]; then
            BASENAME=$(basename "$f")
            if [[ -d "$f" ]]; then
                SIZE="<dir>"
            else
                # Use stat to get file size in KB (rounded)
                if [[ "$OSTYPE" == "darwin"* ]]; then
                    BYTES=$(stat -f%z "$f")
                else
                    BYTES=$(stat -c%s "$f")
                fi
                SIZE_KB=$(( (BYTES + 512) / 1024 ))
                SIZE="${SIZE_KB} KB"
            fi
            printf "  %-30s %12s\n" "$BASENAME" "$SIZE"
        fi
    done

    MAIN_EXE="$OUTPUT_DIR/$EXE_NAME"
    if [[ -f "$MAIN_EXE" ]]; then
        echo ""
        echo "Executable: $MAIN_EXE"
    fi
else
    echo "Warning: Output directory was not created: $OUTPUT_DIR" >&2
fi
