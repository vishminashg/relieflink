import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../state/session.dart';
import '../theme.dart';
import '../widgets/common.dart';
import 'register_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _form = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  ApiException? _error;
  bool _busy = false;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() { _busy = true; _error = null; });
    try {
      await context.read<SessionController>().signIn(_email.text, _password.text);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        body: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 420),
                child: Form(
                  key: _form,
                  child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                    Row(children: [
                      Container(
                        width: 40, height: 40,
                        decoration: BoxDecoration(color: ink, borderRadius: BorderRadius.circular(9)),
                        child: const Icon(Icons.add, color: Colors.white, size: 28),
                      ),
                      const SizedBox(width: 12),
                      Text('ReliefLink', style: Theme.of(context).textTheme.headlineSmall),
                    ]),
                    const SizedBox(height: 28),
                    Text('Sign in', style: Theme.of(context).textTheme.headlineSmall),
                    const SizedBox(height: 4),
                    const Text('Ask for help and see what is happening with your request.'),
                    const SizedBox(height: 20),
                    if (_error != null) ErrorBox(error: _error!),
                    TextFormField(
                      key: const Key('email'),
                      controller: _email,
                      keyboardType: TextInputType.emailAddress,
                      autofillHints: const [AutofillHints.username],
                      decoration: const InputDecoration(labelText: 'Email'),
                      validator: (v) => (v == null || !v.contains('@')) ? 'Enter your email address.' : null,
                    ),
                    const SizedBox(height: 14),
                    TextFormField(
                      key: const Key('password'),
                      controller: _password,
                      obscureText: true,
                      autofillHints: const [AutofillHints.password],
                      decoration: const InputDecoration(labelText: 'Password'),
                      validator: (v) => (v == null || v.isEmpty) ? 'Enter your password.' : null,
                      onFieldSubmitted: (_) => _submit(),
                    ),
                    const SizedBox(height: 20),
                    FilledButton(onPressed: _busy ? null : _submit, child: Text(_busy ? 'Signing in…' : 'Sign in')),
                    const SizedBox(height: 12),
                    OutlinedButton(
                      onPressed: _busy ? null : () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const RegisterScreen())),
                      child: const Text('Create an account'),
                    ),
                  ]),
                ),
              ),
            ),
          ),
        ),
      );
}
