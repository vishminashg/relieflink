import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'api/api_client.dart';
import 'location/location_service.dart';
import 'screens/home_screen.dart';
import 'screens/login_screen.dart';
import 'state/session.dart';
import 'theme.dart';

class ReliefLinkApp extends StatelessWidget {
  const ReliefLinkApp({super.key, required this.session, required this.api, required this.location, this.clock = DateTime.now});

  final SessionController session;
  final ApiClient api;
  final LocationService location;
  final DateTime Function() clock;

  @override
  Widget build(BuildContext context) => MultiProvider(
        providers: [
          ChangeNotifierProvider<SessionController>.value(value: session),
          Provider<ApiClient>.value(value: api),
          Provider<LocationService>.value(value: location),
          Provider<DateTime Function()>.value(value: clock),
        ],
        child: MaterialApp(
          title: 'ReliefLink',
          debugShowCheckedModeBanner: false,
          theme: buildTheme(),
          home: const _Gate(),
        ),
      );
}

/// Shows sign in or the home screen, depending on who is signed in.
class _Gate extends StatelessWidget {
  const _Gate();

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();
    if (!session.ready) return const Scaffold(body: Center(child: CircularProgressIndicator()));
    return session.user == null ? const LoginScreen() : const HomeScreen();
  }
}
