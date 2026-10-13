import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../logic/status_text.dart';
import '../state/session.dart';
import '../theme.dart';
import '../widgets/common.dart';
import 'home_screen.dart';
import 'request_detail_screen.dart';
import 'request_form_screen.dart';

class RequestsScreen extends StatefulWidget {
  const RequestsScreen({super.key});

  @override
  State<RequestsScreen> createState() => _RequestsScreenState();
}

class _RequestsScreenState extends State<RequestsScreen> {
  List<ReliefRequest>? _items;
  ApiException? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final page = await context.read<ApiClient>().myRequests();
      if (mounted) setState(() => _items = page.items);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _newRequest() async {
    final created = await Navigator.of(context).push<ReliefRequest>(MaterialPageRoute(builder: (_) => const RequestFormScreen()));
    if (created != null) _load();
  }

  Future<void> _open(ReliefRequest r) async {
    await Navigator.of(context).push<void>(MaterialPageRoute(builder: (_) => RequestDetailScreen(request: r)));
    _load();
  }

  @override
  Widget build(BuildContext context) {
    final user = context.watch<SessionController>().user!;
    final now = context.read<DateTime Function()>()();
    return Scaffold(
      appBar: AppBar(title: const Text('My requests'), actions: const [SignOutButton()]),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _newRequest,
        icon: const Icon(Icons.add),
        label: const Text('Ask for help', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
          children: [
            Text('Hello, ${user.fullName.split(' ').first}', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 12),
            if (_error != null) ErrorBox(error: _error!, onRetry: _load),
            if (_loading && _items == null)
              const Padding(padding: EdgeInsets.all(40), child: Center(child: CircularProgressIndicator()))
            else if (_items != null && _items!.isEmpty)
              const _Empty()
            else
              for (final r in _items ?? const <ReliefRequest>[]) _RequestCard(request: r, now: now, onTap: () => _open(r)),
          ],
        ),
      ),
    );
  }
}

class _Empty extends StatelessWidget {
  const _Empty();

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(24),
        decoration: BoxDecoration(border: Border.all(color: const Color(0xFFD3DCE2), width: 2), borderRadius: BorderRadius.circular(8), color: Colors.white),
        child: const Column(children: [
          Text('You have not asked for help yet.', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
          SizedBox(height: 6),
          Text('Tap “Ask for help” and tell us what your household needs.', textAlign: TextAlign.center),
        ]),
      );
}

class _RequestCard extends StatelessWidget {
  const _RequestCard({required this.request, required this.now, required this.onTap});

  final ReliefRequest request;
  final DateTime now;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final r = request;
    final closed = isClosedStatus(r.status);
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      elevation: 0,
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8), side: const BorderSide(color: Color(0xFFD3DCE2))),
      child: InkWell(
        onTap: onTap,
        child: Container(
          decoration: BoxDecoration(border: Border(left: BorderSide(width: 8, color: closed ? Colors.black26 : urgencyColor(r.urgency)))),
          padding: const EdgeInsets.all(14),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [
              Expanded(child: Text(r.district, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: ink))),
              StatusPill(r.status),
            ]),
            const SizedBox(height: 4),
            Text(r.addressText, maxLines: 2, overflow: TextOverflow.ellipsis),
            const SizedBox(height: 8),
            Wrap(spacing: 6, runSpacing: 4, children: [
              for (final i in r.items) Chip(label: Text('${i.category} ${i.quantity}'), visualDensity: VisualDensity.compact),
            ]),
            const SizedBox(height: 6),
            Text('Sent ${timeAgo(r.createdAt, now)}', style: const TextStyle(color: Colors.black54)),
          ]),
        ),
      ),
    );
  }
}
