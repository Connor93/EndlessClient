#!/bin/bash
# EndlessClient Release Build Script for Linux/macOS
# Creates a single-file executable with all game assets ready to distribute

set -e  # Exit on error

# Parse arguments
CLEAN=false
OUTPUT_DIR=""
RID=""
CREATE_APP=false
CREATE_TAR=false
CONFIG=""

print_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --clean           Clean output directory before building"
    echo "  --output DIR      Output directory (default: bin/<Config>/SingleFile/<platform>)"
    echo "  --linux           Build for Linux x64"
    echo "  --linux-arm       Build for Linux ARM64"
    echo "  --osx             Build for macOS x64"
    echo "  --osx-arm         Build for macOS ARM64 (Apple Silicon)"
    echo "  --app             Create macOS .app bundle (macOS only)"
    echo "  --tar             Create .tar.gz archive for distribution (Linux)"
    echo "  --debug           Use Debug configuration"
    echo "  --release         Use Release configuration"
    echo "  --help            Show this help message"
    echo ""
    echo "If no platform is specified, the script will auto-detect the current platform."
    echo "Note: macOS defaults to Debug config due to Unity DI reflection issues in Release."
}

while [[ $# -gt 0 ]]; do
    case $1 in
        --clean)
            CLEAN=true
            shift
            ;;
        --output)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --linux)
            RID="linux-x64"
            shift
            ;;
        --linux-arm)
            RID="linux-arm64"
            shift
            ;;
        --osx)
            RID="osx-x64"
            shift
            ;;
        --osx-arm)
            RID="osx-arm64"
            shift
            ;;
        --app)
            CREATE_APP=true
            shift
            ;;
        --tar)
            CREATE_TAR=true
            shift
            ;;
        --debug)
            CONFIG="Debug"
            shift
            ;;
        --release)
            CONFIG="Release"
            shift
            ;;
        --help)
            print_usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            print_usage
            exit 1
            ;;
    esac
done

# Auto-detect platform if not specified
if [ -z "$RID" ]; then
    case "$(uname -s)" in
        Linux*)
            case "$(uname -m)" in
                aarch64|arm64) RID="linux-arm64" ;;
                *) RID="linux-x64" ;;
            esac
            ;;
        Darwin*)
            case "$(uname -m)" in
                arm64) RID="osx-arm64" ;;
                *) RID="osx-x64" ;;
            esac
            ;;
        *)
            echo "Unsupported platform: $(uname -s)"
            exit 1
            ;;
    esac
    echo "Auto-detected platform: $RID"
fi

# Set default config based on platform (macOS has Unity DI issues in Release)
if [ -z "$CONFIG" ]; then
    if [[ "$RID" == osx-* ]]; then
        CONFIG="Debug"
        echo "Using Debug config (default for macOS due to Unity DI issues)"
    else
        CONFIG="Release"
    fi
fi

# Set default output directory if not specified
if [ -z "$OUTPUT_DIR" ]; then
    OUTPUT_DIR="bin/$CONFIG/SingleFile/$RID"
fi

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=== EndlessClient Build ==="
echo "Platform: $RID"
echo "Config: $CONFIG"
echo "Output: $OUTPUT_DIR"
echo ""

# Clean if requested
if [ "$CLEAN" = true ] && [ -d "$OUTPUT_DIR" ]; then
    echo "Cleaning output directory..."
    rm -rf "$OUTPUT_DIR"
fi

# Build executable
echo "Building executable..."
if [[ "$RID" == osx-* ]]; then
    # macOS: Don't use single-file due to native library loading issues with SDL2/OpenAL
    dotnet publish EndlessClient/EndlessClient.csproj \
        -c "$CONFIG" \
        -r "$RID" \
        --self-contained true \
        -o "$OUTPUT_DIR"
else
    # Linux: Use single-file for easier distribution
    dotnet publish EndlessClient/EndlessClient.csproj \
        -c "$CONFIG" \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$OUTPUT_DIR"
fi

if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

# Clean up debug files
echo "Cleaning up debug files..."
rm -f "$OUTPUT_DIR"/*.pdb 2>/dev/null || true
rm -f "$OUTPUT_DIR"/*.config 2>/dev/null || true

# Copy game assets from ClientAssets (overwrite/merge with publish output)
echo "Copying game assets..."
ASSET_FOLDERS=("config" "data" "gfx" "jbox" "maps" "mfx" "pub" "sfx")

for folder in "${ASSET_FOLDERS[@]}"; do
    source="$PROJECT_ROOT/ClientAssets/$folder"
    dest="$OUTPUT_DIR/$folder"
    
    if [ -d "$source" ]; then
        echo "  Copying $folder..."
        mkdir -p "$dest"
        cp -r "$source"/* "$dest"/
    else
        echo "  Warning: $folder not found in ClientAssets"
    fi
done

# Show results
echo ""
echo "=== Build Complete ==="
echo "Output: $(cd "$OUTPUT_DIR" && pwd)"
echo ""

# List output contents with sizes
echo "Contents:"
if command -v numfmt &> /dev/null; then
    # Linux with coreutils
    ls -lh "$OUTPUT_DIR" | tail -n +2 | awk '{printf "  %-40s %s\n", $9, $5}'
else
    # macOS or systems without numfmt
    ls -lh "$OUTPUT_DIR" | tail -n +2 | awk '{printf "  %-40s %s\n", $9, $5}'
fi

echo ""
echo "Ready to distribute!"

# Create macOS .app bundle if requested
if [ "$CREATE_APP" = true ]; then
    # Validate that we're building for macOS
    if [[ "$RID" != osx-* ]]; then
        echo "Error: --app flag is only valid for macOS builds (--osx or --osx-arm)"
        exit 1
    fi
    
    echo ""
    echo "Creating macOS .app bundle..."
    
    APP_NAME="EndlessClient.app"
    APP_DIR="$PROJECT_ROOT/bin/$CONFIG/$APP_NAME"
    CONTENTS_DIR="$APP_DIR/Contents"
    MACOS_DIR="$CONTENTS_DIR/MacOS"
    RESOURCES_DIR="$CONTENTS_DIR/Resources"
    
    # Clean existing .app if present
    if [ -d "$APP_DIR" ]; then
        rm -rf "$APP_DIR"
    fi
    
    # Create directory structure
    mkdir -p "$MACOS_DIR"
    mkdir -p "$RESOURCES_DIR"
    
    # Copy the built files to Resources
    echo "  Copying application files..."
    cp -r "$OUTPUT_DIR/"* "$RESOURCES_DIR/"
    
    # Create launcher script in MacOS directory
    echo "  Creating launcher script..."
    cat > "$MACOS_DIR/EndlessClient" << 'LAUNCHER'
#!/bin/bash
# Launcher script for EndlessClient.app
DIR="$(cd "$(dirname "$0")" && pwd)"
RESOURCES="$DIR/../Resources"
cd "$RESOURCES"
exec "./EndlessClient" "$@"
LAUNCHER
    chmod +x "$MACOS_DIR/EndlessClient"
    
    # Create Info.plist
    echo "  Creating Info.plist..."
    cat > "$CONTENTS_DIR/Info.plist" << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>EndlessClient</string>
    <key>CFBundleDisplayName</key>
    <string>Endless Online Client</string>
    <key>CFBundleIdentifier</key>
    <string>com.endlessonline.client</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>EndlessClient</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.games</string>
</dict>
</plist>
PLIST
    
    # Check for icon file and convert if needed
    ICON_SOURCE="$PROJECT_ROOT/EndlessClient/Icon.ico"
    if [ -f "$ICON_SOURCE" ]; then
        echo "  Processing application icon..."
        # Try to convert .ico to .icns using sips (built into macOS)
        # First extract the largest PNG from the .ico, then create icns
        if command -v sips &> /dev/null && command -v iconutil &> /dev/null; then
            ICONSET_DIR="$RESOURCES_DIR/AppIcon.iconset"
            mkdir -p "$ICONSET_DIR"
            
            # sips can read ico files directly on macOS
            # Create required icon sizes for iconset
            sips -s format png -z 16 16 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_16x16.png" 2>/dev/null || true
            sips -s format png -z 32 32 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_16x16@2x.png" 2>/dev/null || true
            sips -s format png -z 32 32 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_32x32.png" 2>/dev/null || true
            sips -s format png -z 64 64 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_32x32@2x.png" 2>/dev/null || true
            sips -s format png -z 128 128 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_128x128.png" 2>/dev/null || true
            sips -s format png -z 256 256 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_128x128@2x.png" 2>/dev/null || true
            sips -s format png -z 256 256 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_256x256.png" 2>/dev/null || true
            sips -s format png -z 512 512 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_256x256@2x.png" 2>/dev/null || true
            sips -s format png -z 512 512 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_512x512.png" 2>/dev/null || true
            sips -s format png -z 1024 1024 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_512x512@2x.png" 2>/dev/null || true
            
            # Create .icns file
            iconutil -c icns "$ICONSET_DIR" -o "$RESOURCES_DIR/AppIcon.icns" 2>/dev/null && \
                echo "    Icon created successfully" || \
                echo "    Warning: Could not create icon (app will use default icon)"
            
            # Clean up iconset
            rm -rf "$ICONSET_DIR"
        else
            echo "    Warning: iconutil not available, skipping icon conversion"
        fi
    else
        echo "    Warning: No Icon.ico found, app will use default icon"
    fi
    
    echo ""
    echo "=== macOS .app Bundle Created ==="
    echo "Location: $APP_DIR"
    echo ""
    echo "You can now:"
    echo "  1. Double-click EndlessClient.app in Finder to run"
    echo "  2. Drag it to /Applications to install"
    echo "  3. Create a DMG for distribution"
elif [ "$CREATE_TAR" = true ]; then
    # Create tarball for Linux distribution
    if [[ "$RID" != linux-* ]]; then
        echo "Warning: --tar flag is intended for Linux builds"
    fi
    
    echo ""
    echo "Creating distributable tarball..."
    
    TAR_NAME="EndlessClient-$RID.tar.gz"
    TAR_PATH="$PROJECT_ROOT/bin/$CONFIG/$TAR_NAME"
    
    # Create tarball from the output directory
    tar -czf "$TAR_PATH" -C "$(dirname "$OUTPUT_DIR")" "$(basename "$OUTPUT_DIR")"
    
    echo ""
    echo "=== Tarball Created ==="
    echo "Location: $TAR_PATH"
    echo "Size: $(ls -lh "$TAR_PATH" | awk '{print $5}')"
    echo ""
    echo "To distribute:"
    echo "  1. Copy $TAR_NAME to the target Linux machine"
    echo "  2. Extract: tar -xzf $TAR_NAME"
    echo "  3. Run: ./$(basename "$OUTPUT_DIR")/EndlessClient"
else
    echo ""
    echo "To run the game:"
    echo "  cd $OUTPUT_DIR"
    echo "  ./EndlessClient"
    
    if [[ "$RID" == linux-* ]]; then
        echo ""
        echo "To create a distributable tarball, run with --tar flag"
    fi
fi
