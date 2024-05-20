#!/bin/bash

function build_one () {
    SDK="$1"
    DOTNET="net${SDK//./}"
    DOTNET_MACRO="NET${SDK//./}"

    LIB_DIR=dist/c-wspp-websocket-sharp
    DEST_DIR=build/test/$PLATFORM/$DOTNET
    LIB_NAME=websocket-sharp.dll

    LIB="$LIB_DIR/$LIB_NAME"
    if [ -f "$LIB" ]; then
        mkdir -p "$DEST_DIR"

        TEST_NAME=wstest
        SRC="test/$TEST_NAME.cs"
        OUT="$DEST_DIR/$TEST_NAME.exe"
        echo "$TEST_NAME -> $OUT"
        mcs -sdk:$SDK -out:$OUT $SRC -r:$LIB -d:$DOTNET_MACRO

        if [ -f "test/$TEST_NAME.cs" ]; then
            TEST_NAME=multiclient-test
            SRC="test/$TEST_NAME.cs"
            OUT="$DEST_DIR/$TEST_NAME.exe"
            echo "$TEST_NAME -> $OUT"
            mcs -sdk:$SDK -out:$OUT $SRC -r:$LIB -r:test-deps/$DOTNET/Archipelago.MultiClient.Net.dll -r:System.Windows.Forms -d:$DOTNET_MACRO
        fi

        cp $LIB_DIR/* $DEST_DIR/
    else
        echo "Can't build test. $LIB_NAME missing!"
        return 1
    fi
}

build_one 2.0 || exit 1
build_one 4.0 || exit 1
