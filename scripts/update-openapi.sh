#! /usr/bin/env bash

cd "$(git rev-parse --show-toplevel)" || exit

# The generator boots the app to read its routes. The app refuses to start when another instance
# already holds the singleton mutex, and that mutex is keyed on the config folder, so generating
# while ErsatzTV is running fails with "The entry point exited without ever building an IHost".
# Give the generator a config folder of its own rather than have it fight a live instance.
ETV_CONFIG_FOLDER="$(mktemp -d)"
ETV_TRANSCODE_FOLDER="$ETV_CONFIG_FOLDER/transcode"
export ETV_CONFIG_FOLDER ETV_TRANSCODE_FOLDER
trap 'rm -rf "$ETV_CONFIG_FOLDER"' EXIT

cd ErsatzTV && dotnet build -t:GenerateOpenApiDocuments
