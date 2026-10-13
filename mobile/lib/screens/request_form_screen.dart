import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../data/districts.dart';
import '../location/location_service.dart';
import '../logic/request_form.dart';
import '../widgets/common.dart';

/// One choice in the "when do you need it" list.
class _Deadline {
  const _Deadline(this.label, {this.hours, this.fixed});
  final String label;
  final int? hours;
  final DateTime? fixed;

  DateTime? resolve(DateTime now) => fixed ?? (hours == null ? null : now.add(Duration(hours: hours!)));
}

class RequestFormScreen extends StatefulWidget {
  const RequestFormScreen({super.key, this.existing});

  /// Set when the person is changing a request they already sent.
  final ReliefRequest? existing;

  @override
  State<RequestFormScreen> createState() => _RequestFormScreenState();
}

class _RequestFormScreenState extends State<RequestFormScreen> {
  late final RequestDraft _draft = widget.existing == null ? RequestDraft() : RequestDraft.from(widget.existing!);
  late final TextEditingController _address = TextEditingController(text: _draft.addressText);
  late final TextEditingController _notes = TextEditingController(text: _draft.notes);
  late final List<_Deadline> _deadlines;
  late _Deadline _deadline;

  final _scroll = ScrollController();
  Map<String, String> _errors = {};
  ApiException? _error;
  String? _locationMessage;
  bool _locating = false;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    final kept = widget.existing?.neededBy;
    _deadlines = [
      if (kept != null) _Deadline('Keep the time I gave before', fixed: kept),
      const _Deadline('Within 6 hours', hours: 6),
      const _Deadline('Within 12 hours', hours: 12),
      const _Deadline('Within 24 hours', hours: 24),
      const _Deadline('Within 3 days', hours: 72),
      const _Deadline('I am not sure'),
    ];
    _deadline = kept != null ? _deadlines.first : _deadlines.last;
  }

  @override
  void dispose() {
    _address.dispose();
    _notes.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _useLocation() async {
    setState(() { _locating = true; _locationMessage = null; });
    try {
      final here = await context.read<LocationService>().current();
      if (mounted) {
        setState(() {
          if (insideSriLanka(here.latitude, here.longitude)) {
            _draft.latitude = here.latitude;
            _draft.longitude = here.longitude;
            _locationMessage = 'Your location will be sent with the request.';
          } else {
            // A position outside Sri Lanka is never kept: the request falls back to the district centre,
            // so the person is not stuck with a form that cannot be sent.
            _draft.latitude = null;
            _draft.longitude = null;
            _locationMessage = 'Your phone is outside Sri Lanka, so we will use the centre of your district instead.';
          }
        });
      }
    } on LocationUnavailable catch (e) {
      if (mounted) setState(() => _locationMessage = e.message);
    } finally {
      if (mounted) setState(() => _locating = false);
    }
  }

  void _scrollToTop() {
    if (_scroll.hasClients) _scroll.animateTo(0, duration: const Duration(milliseconds: 250), curve: Curves.easeOut);
  }

  Future<void> _submit() async {
    final now = context.read<DateTime Function()>()();
    _draft.addressText = _address.text;
    _draft.notes = _notes.text;
    _draft.neededBy = _deadline.resolve(now);

    final errors = validateDraft(_draft, now);
    setState(() {
      _errors = errors;
      // A summary at the top, so nobody has to hunt down the red messages further down the form.
      _error = errors.isEmpty
          ? null
          : ApiException(0, 'Please fix these before sending:', {'form': errors.values.toList()});
    });
    if (errors.isNotEmpty) {
      _scrollToTop();
      return;
    }

    setState(() => _busy = true);
    final api = context.read<ApiClient>();
    final navigator = Navigator.of(context);
    try {
      final saved = widget.existing == null
          ? await api.createRequest(_draft.toJson())
          : await api.updateRequest(widget.existing!.id, _draft.toJson());
      navigator.pop(saved);
    } on ApiException catch (e) {
      if (mounted) {
        setState(() => _error = e); // everything typed stays on screen
        _scrollToTop(); // the message is at the top, the button was at the bottom
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final editing = widget.existing != null;
    return Scaffold(
      appBar: AppBar(title: Text(editing ? 'Change my request' : 'Ask for help')),
      body: SafeArea(
        child: ListView(controller: _scroll, padding: const EdgeInsets.all(16), children: [
          Container(
            padding: const EdgeInsets.all(12),
            margin: const EdgeInsets.only(bottom: 14),
            decoration: BoxDecoration(color: const Color(0xFFFFF6E0), borderRadius: BorderRadius.circular(8), border: Border.all(color: const Color(0xFFC58A00))),
            child: const Text('If someone’s life is in danger, call 117 (Disaster Management Centre) or 1990 (ambulance) first.'),
          ),
          if (_error != null) ErrorBox(error: _error!),
          SectionCard(title: 'Your household', children: [
            CountField(label: 'People in the household', value: _draft.householdSize, min: 1, max: maxHouseholdSize, onChanged: (v) => setState(() => _draft.householdSize = v)),
            FieldError(_errors['householdSize']),
            CountField(label: 'Elderly people', value: _draft.elderlyCount, min: 0, max: maxHouseholdSize, onChanged: (v) => setState(() => _draft.elderlyCount = v)),
            CountField(label: 'Children', value: _draft.childrenCount, min: 0, max: maxHouseholdSize, onChanged: (v) => setState(() => _draft.childrenCount = v)),
            FieldError(_errors['people']),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Our home is flooded'),
              value: _draft.homeFlooded,
              onChanged: (v) => setState(() => _draft.homeFlooded = v),
            ),
          ]),
          SectionCard(title: 'What you need', children: [
            for (final c in needCategories) _NeedRow(
              category: c,
              quantity: _draft.needs[c],
              onToggle: (on) => setState(() => on ? _draft.needs[c] = 1 : _draft.needs.remove(c)),
              onQuantity: (q) => setState(() => _draft.needs[c] = q),
            ),
            FieldError(_errors['needs']),
          ]),
          SectionCard(title: 'Where you are', children: [
            DropdownButtonFormField<String>(
              key: const Key('district'),
              initialValue: _draft.district,
              decoration: InputDecoration(labelText: 'District', errorText: _errors['district']),
              items: [for (final d in districts) DropdownMenuItem(value: d.name, child: Text(d.name))],
              onChanged: (v) => setState(() => _draft.district = v),
            ),
            const SizedBox(height: 12),
            TextField(
              key: const Key('address'),
              controller: _address,
              minLines: 2,
              maxLines: 3,
              decoration: InputDecoration(labelText: 'Address or nearby landmark', errorText: _errors['addressText']),
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: _locating ? null : _useLocation,
              icon: const Icon(Icons.my_location),
              label: Text(_locating ? 'Finding you…' : 'Use my current location'),
            ),
            if (_locationMessage != null) Padding(padding: const EdgeInsets.only(top: 6), child: Text(_locationMessage!)),
            if (_draft.latitude == null && _draft.district != null)
              Padding(padding: const EdgeInsets.only(top: 6), child: Text('Without your location we use the centre of ${_draft.district}.', style: const TextStyle(color: Colors.black54))),
            FieldError(_errors['location']),
          ]),
          SectionCard(title: 'When do you need help?', children: [
            DropdownButtonFormField<_Deadline>(
              key: const Key('deadline'),
              initialValue: _deadline,
              decoration: InputDecoration(errorText: _errors['neededBy']),
              items: [for (final d in _deadlines) DropdownMenuItem(value: d, child: Text(d.label))],
              onChanged: (v) => setState(() => _deadline = v ?? _deadline),
            ),
          ]),
          SectionCard(title: 'Anything else we should know?', children: [
            TextField(
              key: const Key('notes'),
              controller: _notes,
              minLines: 2,
              maxLines: 5,
              maxLength: 1000,
              decoration: InputDecoration(hintText: 'For example: someone cannot walk, or the road is blocked.', errorText: _errors['notes']),
            ),
          ]),
          FilledButton(
            key: const Key('submit'),
            onPressed: _busy ? null : _submit,
            child: Text(_busy ? 'Sending…' : (editing ? 'Save changes' : 'Send request')),
          ),
        ]),
      ),
    );
  }
}

class _NeedRow extends StatelessWidget {
  const _NeedRow({required this.category, required this.quantity, required this.onToggle, required this.onQuantity});

  final String category;
  final int? quantity;
  final ValueChanged<bool> onToggle;
  final ValueChanged<int> onQuantity;

  @override
  Widget build(BuildContext context) {
    final on = quantity != null;
    return Column(children: [
      CheckboxListTile(
        key: Key('need-$category'),
        contentPadding: EdgeInsets.zero,
        controlAffinity: ListTileControlAffinity.leading,
        title: Text(category, style: const TextStyle(fontSize: 17)),
        value: on,
        onChanged: (v) => onToggle(v ?? false),
      ),
      if (on)
        Padding(
          padding: const EdgeInsets.only(left: 36),
          child: CountField(label: 'How many', value: quantity!, min: 1, max: maxQuantity, onChanged: onQuantity),
        ),
    ]);
  }
}
