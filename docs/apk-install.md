# Install the ReliefLink Android app

## Build the APK
Prerequisites: Flutter SDK, Android SDK (Android Studio installs both).

```bash
cd mobile
flutter pub get
flutter build apk --release --dart-define=API_URL=https://<api>.onrender.com
```

The file is `mobile/build/app/outputs/flutter-apk/app-release.apk`.

## Install on a phone
1. Copy `app-release.apk` to the phone (USB cable, Drive or messaging app).
2. Open the file on the phone. If Android asks, allow installing from this source.
3. Open **ReliefLink**.

Or with a cable and USB debugging on: `adb install -r app-release.apk`.

## First start
The API sleeps when idle on the free tier. The first sign-in can take up to a minute. Wait for it.

## Citizen flow (Component A)
1. Sign in as a citizen (or register).
2. Tap **Ask for help**. Fill in your household, what you need, your district and address, and when you need help.
3. Tap **Send request**. Open the request later to see its status as the coordinator works on it.

## Coordinator flow
Coordinators use the web console. Open the request, press **Draft a plan with AI**, review the proposed plan, then **Approve plan** or **Reject plan**. The citizen's app shows the new status.

## Troubleshooting
| Symptom | Cause and fix |
|---|---|
| Sign-in times out | Service was asleep. Wait a minute and try again. |
| "Network error" at once | Wrong `API_URL` in the build, or phone offline. Rebuild with the correct address. |
| App not installed | An older build signed with another key is installed. Uninstall it first. |
