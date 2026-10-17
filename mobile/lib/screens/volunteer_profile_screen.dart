import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../theme.dart';
import '../widgets/common.dart';

const volunteerSkillsList = [
  'FirstAid',
  'Medical',
  'SearchAndRescue',
  'BoatOperation',
  'HeavyVehicleDriving',
  'FoodDistribution',
  'ChildCare',
  'ElderlyCare',
];

const sriLankaDistricts = [
  'Ampara', 'Anuradhapura', 'Badulla', 'Batticaloa', 'Colombo', 'Galle', 'Gampaha', 'Hambantota', 'Jaffna',
  'Kalutara', 'Kandy', 'Kegalle', 'Kilinochchi', 'Kurunegala', 'Mannar', 'Matale', 'Matara', 'Monaragala',
  'Mullaitivu', 'Nuwara Eliya', 'Polonnaruwa', 'Puttalam', 'Ratnapura', 'Trincomalee', 'Vavuniya',
];

class VolunteerProfileScreen extends StatefulWidget {
  const VolunteerProfileScreen({super.key});

  @override
  State<VolunteerProfileScreen> createState() => _VolunteerProfileScreenState();
}

class _VolunteerProfileScreenState extends State<VolunteerProfileScreen> {
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

  Future<void> _editProfile() async {
    if (_profile == null) return;

    var district = _profile!.district;
    final nameCtrl = TextEditingController(text: _profile!.emergencyContactName);
    final phoneCtrl = TextEditingController(text: _profile!.emergencyContactPhone);
    final notesCtrl = TextEditingController(text: _profile!.notes ?? '');
    var maxHours = _profile!.maxHoursPerWeek;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: const Text('Edit Volunteer Profile'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                DropdownButtonFormField<String>(
                  value: sriLankaDistricts.contains(district) ? district : sriLankaDistricts.first,
                  decoration: const InputDecoration(labelText: 'Base District'),
                  items: sriLankaDistricts.map((d) => DropdownMenuItem(value: d, child: Text(d))).toList(),
                  onChanged: (val) {
                    if (val != null) setDialogState(() => district = val);
                  },
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: nameCtrl,
                  decoration: const InputDecoration(labelText: 'Emergency Contact Name (ICE) *'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: phoneCtrl,
                  keyboardType: TextInputType.phone,
                  decoration: const InputDecoration(labelText: 'Emergency Contact Phone *'),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    const Expanded(child: Text('Max Hours / Week')),
                    DropdownButton<int>(
                      value: maxHours,
                      items: [10, 20, 30, 40, 50, 60]
                          .map((h) => DropdownMenuItem(value: h, child: Text('$h hrs')))
                          .toList(),
                      onChanged: (val) {
                        if (val != null) setDialogState(() => maxHours = val);
                      },
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: notesCtrl,
                  maxLines: 2,
                  decoration: const InputDecoration(labelText: 'Operational Notes / Equipment'),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.of(ctx).pop(false), child: const Text('Cancel')),
            FilledButton(onPressed: () => Navigator.of(ctx).pop(true), child: const Text('Save Profile')),
          ],
        ),
      ),
    );

    if (confirmed == true && mounted) {
      try {
        await context.read<ApiClient>().updateVolunteerProfile(_profile!.id, {
          'district': district,
          'emergencyContactName': nameCtrl.text.trim(),
          'emergencyContactPhone': phoneCtrl.text.trim(),
          'maxHoursPerWeek': maxHours,
          'notes': notesCtrl.text.trim().isNotEmpty ? notesCtrl.text.trim() : null,
        });
        _load();
      } on ApiException catch (e) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(SnackBar(backgroundColor: critical, content: Text(e.message)));
        }
      }
    }
  }

  Future<void> _addSkill() async {
    if (_profile == null) return;

    var skill = volunteerSkillsList.first;
    var proficiency = 'Intermediate';
    final certCtrl = TextEditingController();

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: const Text('Add Certified Skill'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                DropdownButtonFormField<String>(
                  value: skill,
                  decoration: const InputDecoration(labelText: 'Skill Category'),
                  items: volunteerSkillsList
                      .map((s) => DropdownMenuItem(value: s, child: Text(s)))
                      .toList(),
                  onChanged: (val) {
                    if (val != null) setDialogState(() => skill = val);
                  },
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  value: proficiency,
                  decoration: const InputDecoration(labelText: 'Proficiency Level'),
                  items: const ['Beginner', 'Intermediate', 'Advanced', 'Expert']
                      .map((p) => DropdownMenuItem(value: p, child: Text(p)))
                      .toList(),
                  onChanged: (val) {
                    if (val != null) setDialogState(() => proficiency = val);
                  },
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: certCtrl,
                  decoration: const InputDecoration(
                    labelText: 'Certificate / License Reference (Optional)',
                    hintText: 'e.g. SLRC First Aid Cert #2026-44',
                  ),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.of(ctx).pop(false), child: const Text('Cancel')),
            FilledButton(onPressed: () => Navigator.of(ctx).pop(true), child: const Text('Add Skill')),
          ],
        ),
      ),
    );

    if (confirmed == true && mounted) {
      final current = _profile!.skills.where((s) => s.skill != skill).map((s) => s.toJson()).toList();
      current.add({
        'skill': skill,
        'proficiency': proficiency,
        if (certCtrl.text.trim().isNotEmpty) 'certificateReference': certCtrl.text.trim(),
      });

      try {
        await context.read<ApiClient>().updateVolunteerSkills(_profile!.id, current);
        _load();
      } on ApiException catch (e) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(SnackBar(backgroundColor: critical, content: Text(e.message)));
        }
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (_error != null) ErrorBox(error: _error!, onRetry: _load),
          if (_loading && _profile == null)
            const Padding(padding: EdgeInsets.all(40), child: Center(child: CircularProgressIndicator()))
          else if (_profile != null) ...[
            SectionCard(
              title: 'Volunteer Profile',
              children: [
                _FactRow(label: 'Base District', value: _profile!.district),
                _FactRow(
                  label: 'In Case of Emergency (ICE)',
                  value: '${_profile!.emergencyContactName} (${_profile!.emergencyContactPhone})',
                ),
                _FactRow(label: 'Weekly Commitment Limit', value: '${_profile!.maxHoursPerWeek} hrs/week'),
                _FactRow(
                  label: 'Operational Status',
                  value: _profile!.isActive ? 'Active & Ready for Dispatch' : 'Inactive',
                ),
                if (_profile!.notes != null && _profile!.notes!.isNotEmpty)
                  _FactRow(label: 'Notes & Capabilities', value: _profile!.notes!),
                const SizedBox(height: 12),
                Align(
                  alignment: Alignment.centerRight,
                  child: OutlinedButton.icon(
                    onPressed: _editProfile,
                    icon: const Icon(Icons.edit, size: 16),
                    label: const Text('Edit Profile'),
                  ),
                ),
              ],
            ),
            SectionCard(
              title: 'Certified Skills (${_profile!.skills.length})',
              children: [
                if (_profile!.skills.isEmpty)
                  const Text('No skills registered yet.', style: TextStyle(color: Colors.black54))
                else
                  for (final s in _profile!.skills)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 4),
                      child: Row(
                        children: [
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(s.skill, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
                                Text('${s.proficiency}${s.certificateReference != null ? ' • ${s.certificateReference}' : ''}',
                                    style: const TextStyle(color: Colors.black54, fontSize: 13)),
                              ],
                            ),
                          ),
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                            decoration: BoxDecoration(
                              color: s.isVerified ? const Color(0xFFDCEFDC) : const Color(0xFFFFF0CC),
                              borderRadius: BorderRadius.circular(4),
                            ),
                            child: Text(
                              s.isVerified ? 'Verified ✓' : 'Self-declared',
                              style: TextStyle(
                                color: s.isVerified ? const Color(0xFF1D4A1D) : const Color(0xFF5A4200),
                                fontWeight: FontWeight.w700,
                                fontSize: 12,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                const SizedBox(height: 12),
                Align(
                  alignment: Alignment.centerRight,
                  child: FilledButton.icon(
                    style: FilledButton.styleFrom(backgroundColor: signal),
                    onPressed: _addSkill,
                    icon: const Icon(Icons.add, size: 16),
                    label: const Text('Add Certified Skill'),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _FactRow extends StatelessWidget {
  const _FactRow({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label, style: const TextStyle(color: Colors.black54, fontSize: 13)),
            const SizedBox(height: 2),
            Text(value, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15, color: ink)),
          ],
        ),
      );
}
