#!/usr/bin/env bash
# Prepares an Android emulator with a google_apis system image for the end-to-end device tests.
#
# Usage: prepare-android-emulator.sh <CA certificate of the fake provider> [port, default 7443]
# Set ANDROID_SERIAL to choose the emulator when more than one is connected.
#
# - Adds the CA certificate as a user certificate, which Chrome and the device test application trust.
#   Adding it without the settings screen needs root, which google_apis images allow.
# - Turns off the first-run screens and the notification prompt of Chrome, which would stop Custom Tabs before the
#   sign-in page.
# - Forwards the port of the fake provider, so the emulator reaches it at https://localhost:<port>.
#
# Use a test emulator only: it changes the certificates the emulator trusts and the command line of Chrome.
set -euo pipefail

ca_certificate="$1"
port="${2:-7443}"

adb root >/dev/null
adb wait-for-device

hash=$(openssl x509 -subject_hash_old -noout -in "$ca_certificate")
user_store=/data/misc/user/0/cacerts-added
adb shell "mkdir -p $user_store && chown system:system $user_store && chmod 755 $user_store"
adb push "$ca_certificate" "$user_store/$hash.0" >/dev/null
adb shell "chown system:system $user_store/$hash.0 && chmod 644 $user_store/$hash.0 && restorecon -R $user_store"

flags='_ --disable-fre --no-default-browser-check --no-first-run'
adb shell "echo '$flags' > /data/local/chrome-command-line && chmod 644 /data/local/chrome-command-line"
adb shell "echo '$flags' > /data/local/tmp/chrome-command-line && chmod 644 /data/local/tmp/chrome-command-line"
adb shell am set-debug-app --persistent com.android.chrome
# Denies notifications up front, so Chrome does not ask for them over the sign-in page.
adb shell pm revoke com.android.chrome android.permission.POST_NOTIFICATIONS || true
adb shell pm set-permission-flags com.android.chrome android.permission.POST_NOTIFICATIONS user-set user-fixed || true
adb shell am force-stop com.android.chrome

adb reverse "tcp:$port" "tcp:$port" >/dev/null
echo "The emulator trusts the fake provider CA ($hash.0) and reaches it at https://localhost:$port."
