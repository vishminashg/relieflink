import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/session.dart';
import 'requests_screen.dart';

/// Citizens see their requests. Volunteer screens belong to the volunteer component and are added there.
class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final user = context.watch<SessionController>().user!;
    if (user.role == 'Volunteer') {
      return Scaffold(
        appBar: AppBar(title: const Text('ReliefLink'), actions: [_SignOut()]),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Text('Welcome, ${user.fullName}. Volunteer tasks will appear here.', textAlign: TextAlign.center, style: const TextStyle(fontSize: 18)),
          ),
        ),
      );
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
