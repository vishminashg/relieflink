import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../theme.dart';
import '../widgets/common.dart';

class VolunteerAvailabilityScreen extends StatefulWidget {
  const VolunteerAvailabilityScreen({super.key});

  @override
  State<VolunteerAvailabilityScreen> createState() =>
      _VolunteerAvailabilityScreenState();
}

class _VolunteerAvailabilityScreenState
    extends State<VolunteerAvailabilityScreen> {
  VolunteerProfile? _profile;
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
      final p = await context.read<ApiClient>().myVolunteerProfile();
      if (mounted) setState(() => _profile = p);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _addSlot() async {
    if (_profile == null) return;

    var type = 'RecurringWeekly';
    var selectedDay = 'Monday';
    TimeOfDay startTime = const TimeOfDay(hour: 9, minute: 0);
    TimeOfDay endTime = const TimeOfDay(hour: 17, minute: 0);

    DateTime specificDate = DateTime.now().add(const Duration(days: 1));

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: const Text('Add Availability Window'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                DropdownButtonFormField<String>(
                  initialValue: type,
                  decoration:
                      const InputDecoration(labelText: 'Availability Type'),
                  items: const [
                    DropdownMenuItem(
                        value: 'RecurringWeekly',
                        child: Text('Recurring Weekly')),
                    DropdownMenuItem(
                        value: 'SpecificDate',
                        child: Text('Specific Date Window')),
                  ],
                  onChanged: (val) {
                    if (val != null) setDialogState(() => type = val);
                  },
                ),
                const SizedBox(height: 12),
                if (type == 'RecurringWeekly') ...[
                  DropdownButtonFormField<String>(
                    initialValue: selectedDay,
                    decoration: const InputDecoration(labelText: 'Day of Week'),
                    items: const [
                      'Monday',
                      'Tuesday',
                      'Wednesday',
                      'Thursday',
                      'Friday',
                      'Saturday',
                      'Sunday'
                    ]
                        .map((d) => DropdownMenuItem(value: d, child: Text(d)))
                        .toList(),
                    onChanged: (val) {
                      if (val != null) setDialogState(() => selectedDay = val);
                    },
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          onPressed: () async {
                            final picked = await showTimePicker(
                                context: ctx, initialTime: startTime);
                            if (picked != null) {
                              setDialogState(() => startTime = picked);
                            }
                          },
                          child: Text('Start: ${startTime.format(ctx)}'),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: OutlinedButton(
                          onPressed: () async {
                            final picked = await showTimePicker(
                                context: ctx, initialTime: endTime);
                            if (picked != null) {
                              setDialogState(() => endTime = picked);
                            }
                          },
                          child: Text('End: ${endTime.format(ctx)}'),
                        ),
                      ),
                    ],
                  ),
                ] else ...[
                  OutlinedButton.icon(
                    icon: const Icon(Icons.calendar_today, size: 16),
                    onPressed: () async {
                      final picked = await showDatePicker(
                        context: ctx,
                        initialDate: specificDate,
                        firstDate: DateTime.now(),
                        lastDate: DateTime.now().add(const Duration(days: 90)),
                      );
                      if (picked != null) {
                        setDialogState(() => specificDate = picked);
                      }
                    },
                    label: Text(
                        'Date: ${specificDate.day}/${specificDate.month}/${specificDate.year}'),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          onPressed: () async {
                            final picked = await showTimePicker(
                                context: ctx, initialTime: startTime);
                            if (picked != null) {
                              setDialogState(() => startTime = picked);
                            }
                          },
                          child: Text('Start: ${startTime.format(ctx)}'),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: OutlinedButton(
                          onPressed: () async {
                            final picked = await showTimePicker(
                                context: ctx, initialTime: endTime);
                            if (picked != null) {
                              setDialogState(() => endTime = picked);
                            }
                          },
                          child: Text('End: ${endTime.format(ctx)}'),
                        ),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
          actions: [
            TextButton(
                onPressed: () => Navigator.of(ctx).pop(false),
                child: const Text('Cancel')),
            FilledButton(
                onPressed: () => Navigator.of(ctx).pop(true),
                child: const Text('Add Slot')),
          ],
        ),
      ),
    );

    if (confirmed == true && mounted) {
      final current = _profile!.availabilities.map((a) => a.toJson()).toList();
      String fmtTime(TimeOfDay t) =>
          '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}:00';

      if (type == 'RecurringWeekly') {
        current.add({
          'type': 'RecurringWeekly',
          'dayOfWeek': selectedDay,
          'startTime': fmtTime(startTime),
          'endTime': fmtTime(endTime),
        });
      } else {
        final startDt = DateTime.utc(specificDate.year, specificDate.month,
            specificDate.day, startTime.hour, startTime.minute);
        final endDt = DateTime.utc(specificDate.year, specificDate.month,
            specificDate.day, endTime.hour, endTime.minute);
        current.add({
          'type': 'SpecificDate',
          'specificStartUtc': startDt.toIso8601String(),
          'specificEndUtc': endDt.toIso8601String(),
        });
      }

      try {
        await context
            .read<ApiClient>()
            .updateVolunteerAvailability(_profile!.id, current);
        _load();
      } on ApiException catch (e) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(backgroundColor: critical, content: Text(e.message)));
        }
      }
    }
  }

  Future<void> _deleteSlot(String id) async {
    if (_profile == null) return;
    final updated = _profile!.availabilities
        .where((a) => a.id != id)
        .map((a) => a.toJson())
        .toList();
    try {
      await context
          .read<ApiClient>()
          .updateVolunteerAvailability(_profile!.id, updated);
      _load();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(backgroundColor: critical, content: Text(e.message)));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _addSlot,
        icon: const Icon(Icons.add),
        label: const Text('Add Availability',
            style: TextStyle(fontWeight: FontWeight.w700)),
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
          children: [
            Text('My Duty Availability',
                style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 6),
            const Text(
              'Set when you can respond to disasters. The AI and coordinator matching engine will only dispatch you during compatible windows.',
              style: TextStyle(color: Colors.black54),
            ),
            const SizedBox(height: 16),
            if (_error != null) ErrorBox(error: _error!, onRetry: _load),
            if (_loading && _profile == null)
              const Padding(
                  padding: EdgeInsets.all(40),
                  child: Center(child: CircularProgressIndicator()))
            else if (_profile != null && _profile!.availabilities.isEmpty)
              const _EmptyAvailability()
            else ...[
              for (final a in _profile?.availabilities ??
                  const <VolunteerAvailabilityModel>[])
                Card(
                  margin: const EdgeInsets.only(bottom: 8),
                  elevation: 0,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(8),
                    side: const BorderSide(color: Color(0xFFD3DCE2)),
                  ),
                  child: ListTile(
                    leading: Icon(
                      a.type == 'RecurringWeekly' ? Icons.repeat : Icons.event,
                      color: signal,
                    ),
                    title: Text(
                      a.type == 'RecurringWeekly'
                          ? 'Every ${a.dayOfWeek}'
                          : '${a.specificStartUtc?.day}/${a.specificStartUtc?.month}/${a.specificStartUtc?.year}',
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                    subtitle: Text(
                      a.type == 'RecurringWeekly'
                          ? '${a.startTime?.substring(0, 5)} - ${a.endTime?.substring(0, 5)}'
                          : '${a.startTime} - ${a.endTime}',
                    ),
                    trailing: IconButton(
                      icon: const Icon(Icons.delete_outline, color: critical),
                      onPressed: () => _deleteSlot(a.id),
                      tooltip: 'Remove Slot',
                    ),
                  ),
                ),
            ],
          ],
        ),
      ),
    );
  }
}

class _EmptyAvailability extends StatelessWidget {
  const _EmptyAvailability();

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
            Text('No availability windows set.',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
            SizedBox(height: 6),
            Text(
              'Tap "Add Availability" to specify days and times when you are free for emergency dispatch.',
              textAlign: TextAlign.center,
            ),
          ],
        ),
      );
}
