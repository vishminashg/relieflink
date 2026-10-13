import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../data/districts.dart';
import '../state/session.dart';
import '../widgets/common.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _phone = TextEditingController();
  final _password = TextEditingController();
  String? _district;
  bool _hide = true;
  bool _busy = false;
  ApiException? _error;

  @override
  void dispose() {
    for (final c in [_name, _email, _phone, _password]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() { _busy = true; _error = null; });
    final navigator = Navigator.of(context);
    try {
      await context.read<SessionController>().register(
            fullName: _name.text,
            email: _email.text,
            password: _password.text,
            phone: _phone.text,
            district: _district,
          );
      navigator.pop(); // the app now shows the signed-in screen underneath
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Create an account')),
        body: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(20),
            child: Form(
              key: _form,
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                if (_error != null) ErrorBox(error: _error!),
                TextFormField(
                  key: const Key('name'),
                  controller: _name,
                  textCapitalization: TextCapitalization.words,
                  decoration: const InputDecoration(labelText: 'Full name'),
                  validator: (v) => (v == null || v.trim().length < 2) ? 'Enter your full name.' : null,
                ),
                const SizedBox(height: 14),
                TextFormField(
                  key: const Key('reg-email'),
                  controller: _email,
                  keyboardType: TextInputType.emailAddress,
                  decoration: const InputDecoration(labelText: 'Email'),
                  validator: (v) => (v == null || !v.contains('@')) ? 'Enter a valid email address.' : null,
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: _phone,
                  keyboardType: TextInputType.phone,
                  decoration: const InputDecoration(labelText: 'Phone (optional)', helperText: 'Coordinators may call this number about your request.'),
                ),
                const SizedBox(height: 14),
                DropdownButtonFormField<String>(
                  initialValue: _district,
                  decoration: const InputDecoration(labelText: 'Your district (optional)'),
                  items: [for (final d in districts) DropdownMenuItem(value: d.name, child: Text(d.name))],
                  onChanged: (v) => setState(() => _district = v),
                ),
                const SizedBox(height: 14),
                TextFormField(
                  key: const Key('reg-password'),
                  controller: _password,
                  obscureText: _hide,
                  decoration: InputDecoration(
                    labelText: 'Password',
                    helperText: 'At least 8 characters, with a letter and a number.',
                    suffixIcon: IconButton(
                      tooltip: _hide ? 'Show password' : 'Hide password',
                      icon: Icon(_hide ? Icons.visibility : Icons.visibility_off),
                      onPressed: () => setState(() => _hide = !_hide),
                    ),
                  ),
                  validator: (v) {
                    final p = v ?? '';
                    if (p.length < 8) return 'Use at least 8 characters.';
                    if (!p.contains(RegExp(r'[A-Za-z]'))) return 'Add at least one letter.';
                    if (!p.contains(RegExp(r'[0-9]'))) return 'Add at least one number.';
                    return null;
                  },
                ),
                const SizedBox(height: 22),
                FilledButton(onPressed: _busy ? null : _submit, child: Text(_busy ? 'Creating…' : 'Create account')),
              ]),
            ),
          ),
        ),
      );
}
