/// Where the ReliefLink API lives. Override when building:
///   flutter build apk --dart-define=API_URL=https://your-api.example.com
/// The default reaches an API running on the same computer as the Android emulator.
const String apiUrl = String.fromEnvironment('API_URL', defaultValue: 'http://10.0.2.2:5039');
