#!/bin/sh
printf '\033c\033]0;%s\a' ScoundrelWithHelp
base_path="$(dirname "$(realpath "$0")")"
"$base_path/Scoundrel_base_rules.x86_64" "$@"
