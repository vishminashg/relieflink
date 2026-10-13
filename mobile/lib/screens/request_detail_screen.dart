import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../logic/status_text.dart';
import '../widgets/common.dart';
import 'request_form_screen.dart';

class RequestDetailScreen extends StatefulWidget {
  const RequestDetailScreen({super.key, required this.request});
  final ReliefRequest request;

  @override
  State<RequestDetailScreen> createState() => _RequestDetailScreenState();
}

class _RequestDetailScreenState extends State<RequestDetailScreen> {
  late ReliefRequest _request = widget.request;
  ApiException? _error;
  bool _busy = false;

  Future<void> _refresh({bool keepError = false}) async {
    try {
      final fresh = await context.read<ApiClient>().request(_request.id);
      if (mounted) setState(() { _request = fresh; if (!keepError) _error = null; });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e);
    }
  }

  Future<void> _edit() async {
    final updated = await Navigator.of(context).push<ReliefRequest>(MaterialPageRoute(builder: (_) => RequestFormScreen(existing: _request)));
    if (updated != null && mounted) setState(() => _request = updated);
  }

  Future<void> _cancel() async {
    // The dialog owns its text field, so nothing is disposed while it is still animating away.
    final reason = await showDialog<String>(context: context, builder: (_) => const _CancelDialog());
    if (reason == null || !mounted) return;

    setState(() { _busy = true; _error = null; });
    try {
      final updated = await context.read<ApiClient>().cancelRequest(_request.id, reason);
      if (mounted) setState(() => _request = updated);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e);
      await _refresh(keepError: true); // the real state may have changed under us; keep the message
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final r = _request;
    final now = context.read<DateTime Function()>()();
    final closed = isClosedStatus(r.status);
    return Scaffold(
      appBar: AppBar(title: Text(r.district)),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: ListView(padding: const EdgeInsets.all(16), children: [
          if (_error != null) ErrorBox(error: _error!),
          Wrap(spacing: 8, runSpacing: 6, children: [StatusPill(r.status), if (!closed) UrgencyChip(r.urgency)]),
          const SizedBox(height: 10),
          Text(statusNextStep(r.status), style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
          if (r.cancellationReason != null) Padding(padding: const EdgeInsets.only(top: 6), child: Text('Reason given: ${r.cancellationReason}')),
          const SizedBox(height: 16),
          if (r.status != 'Rejected' && r.status != 'Cancelled') SectionCard(title: 'Progress', children: [StatusStepper(status: r.status)]),
          SectionCard(title: 'What you asked for', children: [
            for (final i in r.items)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 3),
                child: Text('${i.category}: ${i.quantity}${i.description == null ? '' : ' (${i.description})'}', style: const TextStyle(fontSize: 16)),
              ),
            if (r.notes != null && r.notes!.isNotEmpty) ...[const SizedBox(height: 8), Text('Your notes: ${r.notes}')],
          ]),
          SectionCard(title: 'Your household', children: [
            Text('${r.householdSize} people, including ${r.elderlyCount} elderly and ${r.childrenCount} children.'),
            Text(r.homeFlooded ? 'Your home is flooded.' : 'Your home is not flooded.'),
            const SizedBox(height: 8),
            Text('${r.addressText}, ${r.district}'),
            Text('Sent ${timeAgo(r.createdAt, now)}', style: const TextStyle(color: Colors.black54)),
          ]),
          if (r.canEdit) OutlinedButton(onPressed: _busy ? null : _edit, child: const Text('Change my request')),
          if (r.canEdit && r.canCancel) const SizedBox(height: 10),
          if (r.canCancel)
            OutlinedButton(
              style: OutlinedButton.styleFrom(foregroundColor: const Color(0xFFB3261E), side: const BorderSide(color: Color(0xFFB3261E))),
              onPressed: _busy ? null : _cancel,
              child: const Text('Cancel this request'),
            ),
        ]),
      ),
    );
  }
}

class _CancelDialog extends StatefulWidget {
  const _CancelDialog();

  @override
  State<_CancelDialog> createState() => _CancelDialogState();
}

class _CancelDialogState extends State<_CancelDialog> {
  final _reason = TextEditingController();

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: const Text('Cancel this request?'),
        content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Coordinators will stop working on it. You can send a new request later.'),
          const SizedBox(height: 12),
          TextField(key: const Key('cancel-reason'), controller: _reason, decoration: const InputDecoration(labelText: 'Reason (optional)'), maxLength: 500),
        ]),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Keep request')),
          FilledButton(
            style: FilledButton.styleFrom(minimumSize: const Size(0, 44), backgroundColor: const Color(0xFFB3261E)),
            onPressed: () => Navigator.pop(context, _reason.text),
            child: const Text('Cancel request'),
          ),
        ],
      );
}
