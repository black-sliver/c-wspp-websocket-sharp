#!/bin/bash

# NOTE: this depends on how the native dll was built and has to match
# 32bit windows gcc uses cdecl by default, msvc uses stdcall by default
# other OSes and architectures work automatically
WIN32_CALLING_CONVENTION=CDECL

if [ "$1" = "debug" ] || [ "$1" == "DEBUG" ]; then
    DEBUG_MACRO="DEBUG"
else
    DEBUG_MACRO="NDEBUG"
fi

LIB_NAME="websocket-sharp.dll"
SRC=src/*
BUILD_DIR=build
DIST_DIR=dist/c-wspp-websocket-sharp

mkdir -p "$BUILD_DIR"
mkdir -p "$DIST_DIR"

echo "build -> $BUILD_DIR/$LIB_NAME | $WIN32_CALLING_CONVENTION"
mcs -sdk:2.0 -target:library -out:$BUILD_DIR/$LIB_NAME $SRC \
    -d:WIN32_C_WSPP_CALLING_CONVENTION_$WIN32_CALLING_CONVENTION -d:$DEBUG_MACRO || exit 1

echo "$BUILD_DIR/$LIB_NAME -> $DIST_DIR/$LIB_NAME"
cp $BUILD_DIR/$LIB_NAME $DIST_DIR/$LIB_NAME

function unpack () {
    URL="$1"
    TMP_DL_NAME="$2"
    DL_LIB_NAME="$3"
    FINAL_LIB_NAME="$4"

    if [ ! -f "$DIST_DIR/$FINAL_LIB_NAME" ]; then
        if [ ! -f "$BUILD_DIR/$TMP_DL_NAME" ]; then
            wget "$URL" -O "$BUILD_DIR/$TMP_DL_NAME"
        fi
        if [[ $TMP_DL_NAME == *.zip ]]; then
            unzip -p "$BUILD_DIR/$TMP_DL_NAME" "$DL_LIB_NAME" > "$DIST_DIR/$FINAL_LIB_NAME"
        else
            echo "  $BUILD_DIR/$TMP_DL_NAME/$DL_LIB_NAME -> $DIST_DIR/$FINAL_LIB_NAME"
            tar -xOJvf "$BUILD_DIR/$TMP_DL_NAME" "$DL_LIB_NAME" > "$DIST_DIR/$FINAL_LIB_NAME"
        fi
    fi
}

# NOTE: we don't have releases for c-wspp at the moment,
# so we download the old builds from c-wspp-websocket-sharp to extract the native code
unpack "https://github.com/black-sliver/c-wspp-websocket-sharp/releases/download/v0.4.1/c-wspp-websocket-sharp_linux-x86_64-openssl1.tar.xz" \
    "c-wspp-openssl1-linux-am64.tar.xz" "build/linux-x86_64/c-wspp.so" "c-wspp-openssl1-linux-amd64.so"
unpack "https://github.com/black-sliver/c-wspp-websocket-sharp/releases/download/v0.4.1/c-wspp-websocket-sharp_linux-x86_64-openssl3.tar.xz" \
    "c-wspp-linux-am64.tar.xz" "build/linux-x86_64/c-wspp.so" "c-wspp-linux-amd64.so"
unpack "https://github.com/black-sliver/c-wspp-websocket-sharp/releases/download/v0.4.1/c-wspp-websocket-sharp_windows-clang32.zip" \
    "c-wspp-win32.zip" "build/win32/c-wspp.dll" "c-wspp-win32.dll"
unpack "https://github.com/black-sliver/c-wspp-websocket-sharp/releases/download/v0.4.1/c-wspp-websocket-sharp_windows-clang64.zip" \
    "c-wspp-win64.zip" "build/win64/c-wspp.dll" "c-wspp-win64.dll"

# copy licenses
LICENSE="$DIST_DIR/c-wspp-LICENSE.txt"
cat c-wspp/LICENSE > "$LICENSE"
echo "Or read below." >> "$LICENSE"
echo -e "\n---\n\nASIO\n" >> "$LICENSE"
cat c-wspp/subprojects/asio/COPYING >> "$LICENSE"
echo -e "\n---\n\nWebSocket++\n" >> "$LICENSE"
cat c-wspp/subprojects/websocketpp/websocketpp/COPYING >> "$LICENSE"
