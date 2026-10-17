import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/session.dart';
import 'requests_screen.dart';
import 'volunteer_home_screen.dart';

/// Citizens see their requests. Volunteers see their operational dashboard.
class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final user = context.watch<SessionController>().user!;
    if (user.role == 'Volunteer') {
      return const VolunteerHomeScreen();
    }
    return const RequestsScreen();
  }
}

class _SignOut extends StatelessWidget {
  @override
  Widget build(BuildContext context) => TextButton(
        onPressed: () => context.read<SessionController>().signOut(),
        child: const Text('Sign out', style: TextStyle(color: Colors.white)),
      );
}

class SignOutButton extends StatelessWidget {
  const SignOutButton({super.key});

  @override
  Widget build(BuildContext context) => _SignOut();
}
