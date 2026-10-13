#!/bin/bash
set -e

DEPLOY_DIR="./Deploy"
EXE_NAME="ProctoringClient"
LANDING_DIR="../Landing"

echo "Cleaning deploy folder..."
rm -rf $DEPLOY_DIR
mkdir -p $DEPLOY_DIR

echo "Copying executable..."
cp ./Release/$EXE_NAME $DEPLOY_DIR/

echo "Running linuxdeployqt and building AppImage..."
linuxdeployqt $DEPLOY_DIR/$EXE_NAME -appimage

echo "Moving AppImage to Landing folder..."
mv *.AppImage $LANDING_DIR/

echo "Done!"