import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/session.dart';
import 'inventory_screen.dart';
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
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('Welcome, ${user.fullName}.', textAlign: TextAlign.center, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
                const SizedBox(height: 8),
                const Text('Disaster relief response & emergency logistics.', textAlign: TextAlign.center, style: TextStyle(fontSize: 15, color: Colors.grey)),
                const SizedBox(height: 24),
                ElevatedButton.icon(
                  onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const InventoryScreen())),
                  icon: const Icon(Icons.warehouse),
                  label: const Text('View Depots & Supplies'),
                  style: ElevatedButton.styleFrom(padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12)),
                ),
              ],
            ),
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
