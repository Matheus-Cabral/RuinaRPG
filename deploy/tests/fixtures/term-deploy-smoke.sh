#!/usr/bin/env bash
# Stands in for the smoke in deploy.bats: sends SIGTERM to the deploy.sh that runs it (an operator's
# Ctrl-C / a stopped job), after nginx already switched to the new color. Walks up the process tree
# because run_smoke may wrap the command (timeout).
p=$PPID
until tr '\0' ' ' < "/proc/$p/cmdline" | grep -q 'deploy\.sh deploy'; do
  p="$(ps -o ppid= -p "$p" | tr -d ' ')"
  (( p > 1 )) || { echo "deploy.sh não encontrado" >&2; exit 1; }
done
kill -TERM "$p"
