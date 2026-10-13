import 'package:flutter/widgets.dart';

import 'api/api_client.dart';
import 'app.dart';
import 'config.dart';
import 'data/token_store.dart';
import 'location/location_service.dart';
import 'state/session.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final api = ApiClient(baseUrl: apiUrl);
  final session = SessionController(api: api, store: SecureTokenStore());
  await session.restore();
  runApp(ReliefLinkApp(session: session, api: api, location: DeviceLocationService()));
}
