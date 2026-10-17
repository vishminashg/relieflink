import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../widgets/common.dart';

class NearbySheltersScreen extends StatefulWidget {
  const NearbySheltersScreen({
    super.key,
    required this.latitude,
    required this.longitude,
    required this.district,
  });

  final double latitude;
  final double longitude;
  final String district;

  @override
  State<NearbySheltersScreen> createState() => _NearbySheltersScreenState();
}

class _NearbySheltersScreenState extends State<NearbySheltersScreen> {
  List<Shelter>? _shelters;
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
      final shelters = await context.read<ApiClient>().nearestShelters(
        latitude: widget.latitude,
        longitude: widget.longitude,
      );
      if (mounted) setState(() => _shelters = shelters);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Nearby shelters')),
    body: RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            'Safe places near ${widget.district}',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 6),
          const Text(
            'Availability can change quickly. Refresh before travelling.',
          ),
          const SizedBox(height: 16),
          if (_error != null) ErrorBox(error: _error!, onRetry: _load),
          if (_loading && _shelters == null)
            const Padding(
              padding: EdgeInsets.all(32),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (_shelters != null && _shelters!.isEmpty)
            const SectionCard(
              title: 'No available shelter found',
              children: [
                Text(
                  'Contact your coordinator or call 117 for immediate assistance.',
                ),
              ],
            )
          else
            for (final shelter in _shelters ?? const <Shelter>[])
              _ShelterCard(shelter: shelter),
        ],
      ),
    ),
  );
}

class _ShelterCard extends StatelessWidget {
  const _ShelterCard({required this.shelter});

  final Shelter shelter;

  @override
  Widget build(BuildContext context) => Card(
    margin: const EdgeInsets.only(bottom: 12),
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            shelter.name,
            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 4),
          Text('${shelter.address}, ${shelter.district}'),
          const SizedBox(height: 10),
          Text(
            '${shelter.available} spaces available',
            style: const TextStyle(fontWeight: FontWeight.w700),
          ),
          Text(
            '${shelter.occupied} of ${shelter.capacity} spaces occupied',
            style: const TextStyle(color: Colors.black54),
          ),
        ],
      ),
    ),
  );
}
