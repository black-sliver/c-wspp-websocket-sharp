#!/bin/sh

cd c-wspp
./build.sh
cd ..

LIB_NAME="websocket-sharp.dll"
SRC=src/*

function build_one () {
    # PLATFORM, NATIVE_EXT, OS_MACRO
    PLATFORM="$1"
    OS_MACRO="$3"
    NATIVE_LIB_NAME="c-wspp$2"
    NATIVE_LIB="c-wspp/build/$PLATFORM/lib/$NATIVE_LIB_NAME"
    NATIVE_DEP_DIR="c-wspp/deps/$PLATFORM"
    DEST_DIR=build/$PLATFORM

    if [ -f "$NATIVE_LIB" ]; then
        mkdir -p "$DEST_DIR"
        mcs -sdk:2.0 -target:library -out:$DEST_DIR/$LIB_NAME $SRC -d:$OS_MACRO
        cp $NATIVE_LIB $DEST_DIR
        if [ -d $NATIVE_DEP_DIR ]; then
            cp $NATIVE_DEP_DIR/* $DEST_DIR
        fi
    else
        echo "Skipping $PLATFORM build. $PLATFORM $NATIVE_LIB_NAME missing."
    fi
}

build_one win32 .dll OS_WINDOWS
build_one win64 .dll OS_WINDOWS
build_one linux-x86_64 .so OS_LINUX
build_one macos-x86_64 .dylib OS_MAC

