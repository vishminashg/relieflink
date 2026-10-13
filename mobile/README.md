# ReliefLink mobile app

Flutter app for **citizens** (and, in later components, volunteers). A citizen can create an account, ask for help, follow what is happening with the request, change it while nobody has started on it, and cancel it. Coordinators use the web console instead and are refused here.

## Get the installable APK (no Flutter needed on your computer)
1. Open the repository on GitHub, then **Actions**, then the latest **Mobile CI** run on `main`.
2. Scroll to **Artifacts** and download `relieflink-android-apk`.
3. Unzip it and copy `app-release.apk` to an Android phone, then open it to install (allow "install unknown apps" when asked).

The APK is signed with a debug key. That is fine for this coursework, not for the Play Store.

## Run it yourself
Prerequisites: Flutter 3.47 (stable), Android Studio or the Android SDK, an emulator or a phone, and the API running.

```bash
cd mobile
flutter pub get
flutter run                                   # emulator: the API is reached at http://10.0.2.2:5039
flutter run --dart-define=API_URL=https://your-api.example.com    # a deployed API
```

`10.0.2.2` is how the Android emulator reaches the computer it runs on. A real phone needs the deployed (https) address. Plain http is allowed only for `10.0.2.2` and `localhost` (`android/app/src/main/res/xml/network_security_config.xml`).

## Checks
| Command | What it does |
|---|---|
| `flutter analyze` | Static analysis, must report no issues |
| `flutter test` | 68 unit and widget tests, no network needed |
| `dart run tool/live_check.dart --api http://localhost:5039` | Walks the whole citizen journey against a **real running API** |
| `flutter build apk --release` | Builds the APK into `build/app/outputs/flutter-apk/` |

Design notes and viva questions: [docs/components/A-mobile-app.md](../docs/components/A-mobile-app.md)
