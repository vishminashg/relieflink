import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../theme.dart';
import '../widgets/common.dart';

class VolunteerAssignmentsScreen extends StatefulWidget {
  const VolunteerAssignmentsScreen({super.key});

  @override
  State<VolunteerAssignmentsScreen> createState() =>
      _VolunteerAssignmentsScreenState();
}

class _VolunteerAssignmentsScreenState
    extends State<VolunteerAssignmentsScreen> {
  List<VolunteerAssignmentModel>? _assignments;
  ApiException? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final items = await context.read<ApiClient>().myAssignments();
      if (mounted) setState(() => _assignments = items);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _updateStatus(VolunteerAssignmentModel a, String status,
      {String? notes}) async {
    try {
      await context
          .read<ApiClient>()
          .updateAssignmentStatus(a.id, status, notes: notes);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Mission status updated to $status')),
        );
      }
      _load();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(backgroundColor: critical, content: Text(e.message)),
        );
      }
    }
  }

  Future<void> _promptComplete(VolunteerAssignmentModel a) async {
    final controller = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Complete Field Mission'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
                'Confirm that you have completed this operational deployment.'),
            const SizedBox(height: 12),
            TextField(
              controller: controller,
              maxLines: 2,
              decoration: const InputDecoration(
                labelText: 'Debrief / Completion Notes (Optional)',
                hintText: 'e.g. Distributed 20 water kits, all families safe',
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.of(ctx).pop(false),
              child: const Text('Cancel')),
          FilledButton(
            onPressed: () => Navigator.of(ctx).pop(true),
            child: const Text('Confirm Completed'),
          ),
        ],
      ),
    );

    if (confirmed == true) {
      final note = controller.text.trim();
      await _updateStatus(a, 'Completed', notes: note.isNotEmpty ? note : null);
    }
  }

  Future<void> _promptCancel(VolunteerAssignmentModel a) async {
    final controller = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Unable to Attend Mission'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
                'Please provide an operational reason for withdrawing from this assignment so coordinators can re-dispatch.'),
            const SizedBox(height: 12),
            TextField(
              controller: controller,
              maxLines: 2,
              decoration: const InputDecoration(
                labelText: 'Reason for Withdrawal *',
                hintText: 'e.g. Flooded access road, vehicle breakdown',
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.of(ctx).pop(false),
              child: const Text('Dismiss')),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: critical),
            onPressed: () => Navigator.of(ctx).pop(true),
            child: const Text('Withdraw from Mission'),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) return;

    final reason = controller.text.trim();
    try {
      await context.read<ApiClient>().cancelAssignment(
          a.id, reason.isNotEmpty ? reason : 'Volunteer withdrew');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Assignment cancelled.')),
        );
      }
      _load();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(backgroundColor: critical, content: Text(e.message)),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final active = _assignments
            ?.where((a) => a.status == 'Assigned' || a.status == 'InProgress')
            .toList() ??
        [];
    final history = _assignments
            ?.where((a) => a.status == 'Completed' || a.status == 'Cancelled')
            .toList() ??
        [];

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (_error != null) ErrorBox(error: _error!, onRetry: _load),
          if (_loading && _assignments == null)
            const Padding(
                padding: EdgeInsets.all(40),
                child: Center(child: CircularProgressIndicator()))
          else if (_assignments != null && _assignments!.isEmpty)
            const _EmptyAssignments()
          else ...[
            Text('Active Missions (${active.length})',
                style: Theme.of(context)
                    .textTheme
                    .titleLarge
                    ?.copyWith(fontWeight: FontWeight.w800)),
            const SizedBox(height: 8),
            if (active.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Text(
                    'No field assignments currently active. You are on standby.',
                    style: TextStyle(color: Colors.black54)),
              )
            else
              for (final a in active)
                _AssignmentCard(
                  assignment: a,
                  onStart: () => _updateStatus(a, 'InProgress'),
                  onComplete: () => _promptComplete(a),
                  onCancel: () => _promptCancel(a),
                ),
            const SizedBox(height: 20),
            Text('Past Deployments (${history.length})',
                style: Theme.of(context)
                    .textTheme
                    .titleLarge
                    ?.copyWith(fontWeight: FontWeight.w800)),
            const SizedBox(height: 8),
            if (history.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Text('No historical deployment records yet.',
                    style: TextStyle(color: Colors.black54)),
              )
            else
              for (final a in history) _AssignmentCard(assignment: a),
          ],
        ],
      ),
    );
  }
}

class _AssignmentCard extends StatelessWidget {
  const _AssignmentCard({
    required this.assignment,
    this.onStart,
    this.onComplete,
    this.onCancel,
  });

  final VolunteerAssignmentModel assignment;
  final VoidCallback? onStart;
  final VoidCallback? onComplete;
  final VoidCallback? onCancel;

  @override
  Widget build(BuildContext context) {
    final good =
        assignment.status == 'Completed' || assignment.status == 'InProgress';
    final bad = assignment.status == 'Cancelled';
    final statusBg = good
        ? const Color(0xFFDCEFDC)
        : bad
            ? const Color(0xFFF1E3E2)
            : const Color(0xFFE0F2FE);

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(8),
        side: const BorderSide(color: Color(0xFFD3DCE2)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Text(
                    assignment.roleOrTask ?? 'General Relief Duty',
                    style: const TextStyle(
                        fontSize: 17, fontWeight: FontWeight.w800, color: ink),
                  ),
                ),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
                  decoration: BoxDecoration(
                      color: statusBg, borderRadius: BorderRadius.circular(4)),
                  child: Text(
                    assignment.status,
                    style: const TextStyle(
                        color: ink, fontWeight: FontWeight.w700, fontSize: 13),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                const Icon(Icons.schedule, size: 16, color: Colors.black54),
                const SizedBox(width: 6),
                Text(
                  '${_formatDate(assignment.startTime)} → ${_formatTime(assignment.endTime)}',
                  style: const TextStyle(fontSize: 14, color: Colors.black87),
                ),
              ],
            ),
            if (assignment.notes != null && assignment.notes!.isNotEmpty) ...[
              const SizedBox(height: 6),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                    color: const Color(0xFFF3F6F7),
                    borderRadius: BorderRadius.circular(6)),
                child: Text(assignment.notes!,
                    style:
                        const TextStyle(fontSize: 13, color: Colors.black87)),
              ),
            ],
            if (assignment.canStart ||
                assignment.canComplete ||
                assignment.canCancel) ...[
              const Divider(height: 20),
              Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  if (assignment.canCancel && onCancel != null)
                    TextButton(
                      style: TextButton.styleFrom(foregroundColor: critical),
                      onPressed: onCancel,
                      child: const Text('Withdraw'),
                    ),
                  const SizedBox(width: 8),
                  if (assignment.canStart && onStart != null)
                    FilledButton.icon(
                      style: FilledButton.styleFrom(backgroundColor: signal),
                      onPressed: onStart,
                      icon: const Icon(Icons.play_arrow, size: 18),
                      label: const Text('Start Mission'),
                    ),
                  if (assignment.canComplete && onComplete != null)
                    FilledButton.icon(
                      style: FilledButton.styleFrom(
                          backgroundColor: const Color(0xFF2F7D32)),
                      onPressed: onComplete,
                      icon: const Icon(Icons.check_circle_outline, size: 18),
                      label: const Text('Complete Mission'),
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  String _formatDate(DateTime dt) {
    return '${dt.day}/${dt.month} ${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
  }

  String _formatTime(DateTime dt) {
    return '${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
  }
}

class _EmptyAssignments extends StatelessWidget {
  const _EmptyAssignments();

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(24),
        decoration: BoxDecoration(
          border: Border.all(color: const Color(0xFFD3DCE2), width: 2),
          borderRadius: BorderRadius.circular(8),
          color: Colors.white,
        ),
        child: const Column(
          children: [
            Text('No assignments yet.',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
            SizedBox(height: 6),
            Text(
              'When coordinators match you with nearby relief requests or staging operations, your tasks will appear here.',
              textAlign: TextAlign.center,
            ),
          ],
        ),
      );
}
