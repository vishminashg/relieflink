import 'package:flutter/material.dart';

import '../api/api_client.dart';
import '../logic/status_text.dart';
import '../theme.dart';

class ErrorBox extends StatelessWidget {
  const ErrorBox({super.key, required this.error, this.onRetry});

  final ApiException error;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final details = error.fields.values.expand((e) => e).toList();
    return Semantics(
      liveRegion: true,
      child: Container(
        width: double.infinity,
        margin: const EdgeInsets.only(bottom: 16),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: const Color(0xFFFDECEB),
          border: Border.all(color: const Color(0xFFE3A6A1)),
          borderRadius: BorderRadius.circular(8),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(error.message, style: const TextStyle(color: Color(0xFF6B1510), fontWeight: FontWeight.w700)),
          for (final d in details) Padding(padding: const EdgeInsets.only(top: 4), child: Text('• $d', style: const TextStyle(color: Color(0xFF6B1510)))),
          if (onRetry != null) Padding(padding: const EdgeInsets.only(top: 8), child: OutlinedButton(onPressed: onRetry, child: const Text('Try again'))),
        ]),
      ),
    );
  }
}

class UrgencyChip extends StatelessWidget {
  const UrgencyChip(this.urgency, {super.key});
  final String urgency;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
        decoration: BoxDecoration(color: urgencyColor(urgency), borderRadius: BorderRadius.circular(4)),
        child: Text('$urgency priority', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: 13)),
      );
}

class StatusPill extends StatelessWidget {
  const StatusPill(this.status, {super.key});
  final String status;

  @override
  Widget build(BuildContext context) {
    final good = status == 'Fulfilled' || status == 'Approved' || status == 'InProgress';
    final bad = status == 'Rejected' || status == 'Cancelled';
    final background = good ? const Color(0xFFDCEFDC) : bad ? const Color(0xFFF1E3E2) : const Color(0xFFE4ECF0);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
      decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(4)),
      child: Text(statusLabel(status), style: const TextStyle(color: ink, fontWeight: FontWeight.w700, fontSize: 13)),
    );
  }
}

/// Vertical progress for an open request. This really is a sequence, so the steps are in order.
class StatusStepper extends StatelessWidget {
  const StatusStepper({super.key, required this.status});
  final String status;

  @override
  Widget build(BuildContext context) {
    final current = statusSteps.indexOf(status);
    return Column(children: [
      for (var i = 0; i < statusSteps.length; i++)
        Semantics(
          label: '${statusLabel(statusSteps[i])}, ${i < current || status == 'Fulfilled' ? 'done' : i == current ? 'current step' : 'not yet'}',
          excludeSemantics: true,
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 6),
            child: Row(children: [
              _Dot(done: i < current || status == 'Fulfilled', current: i == current && status != 'Fulfilled'),
              const SizedBox(width: 12),
              Text(
                statusLabel(statusSteps[i]),
                style: TextStyle(fontSize: 16, fontWeight: i == current ? FontWeight.w800 : FontWeight.w400, color: i > current ? Colors.black54 : ink),
              ),
            ]),
          ),
        ),
    ]);
  }
}

class _Dot extends StatelessWidget {
  const _Dot({required this.done, required this.current});
  final bool done;
  final bool current;

  @override
  Widget build(BuildContext context) => Container(
        width: 26,
        height: 26,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: done ? const Color(0xFF2F7D32) : current ? signal : Colors.white,
          border: Border.all(color: done ? const Color(0xFF2F7D32) : current ? signal : Colors.black38, width: 2),
        ),
        child: done ? const Icon(Icons.check, size: 16, color: Colors.white) : null,
      );
}

class SectionCard extends StatelessWidget {
  const SectionCard({super.key, required this.title, required this.children});
  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) => Card(
        margin: const EdgeInsets.only(bottom: 14),
        elevation: 0,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8), side: const BorderSide(color: Color(0xFFD3DCE2))),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(title, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontSize: 18)),
            const SizedBox(height: 12),
            ...children,
          ]),
        ),
      );
}

/// A number with big − and + buttons: easier than typing on a phone in a hurry.
class CountField extends StatelessWidget {
  const CountField({super.key, required this.label, required this.value, required this.min, required this.max, required this.onChanged});

  final String label;
  final int value;
  final int min;
  final int max;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(children: [
          Expanded(child: Text(label, style: const TextStyle(fontSize: 16))),
          IconButton.outlined(
            tooltip: 'Fewer: $label',
            onPressed: value > min ? () => onChanged(value - 1) : null,
            icon: const Icon(Icons.remove),
          ),
          SizedBox(width: 48, child: Text('$value', textAlign: TextAlign.center, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800))),
          IconButton.outlined(
            tooltip: 'More: $label',
            onPressed: value < max ? () => onChanged(value + 1) : null,
            icon: const Icon(Icons.add),
          ),
        ]),
      );
}

class FieldError extends StatelessWidget {
  const FieldError(this.message, {super.key});
  final String? message;

  @override
  Widget build(BuildContext context) => message == null
      ? const SizedBox.shrink()
      : Padding(padding: const EdgeInsets.only(top: 6), child: Text(message!, style: const TextStyle(color: critical, fontWeight: FontWeight.w700)));
}
