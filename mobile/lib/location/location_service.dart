import 'package:geolocator/geolocator.dart';

class LocationUnavailable implements Exception {
  LocationUnavailable(this.message);
  final String message;
  @override
  String toString() => message;
}

abstract class LocationService {
  /// The phone's current position, or throws [LocationUnavailable] with a message a person can act on.
  Future<({double latitude, double longitude})> current();
}

class DeviceLocationService implements LocationService {
  @override
  Future<({double latitude, double longitude})> current() async {
    if (!await Geolocator.isLocationServiceEnabled()) {
      throw LocationUnavailable('Location is switched off on this phone. Switch it on, or choose your district instead.');
    }
    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) permission = await Geolocator.requestPermission();
    if (permission == LocationPermission.denied || permission == LocationPermission.deniedForever) {
      throw LocationUnavailable('Location permission was not given. Choose your district instead.');
    }
    try {
      final p = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(accuracy: LocationAccuracy.high, timeLimit: Duration(seconds: 12)),
      );
      return (latitude: p.latitude, longitude: p.longitude);
    } catch (_) {
      throw LocationUnavailable('Could not find your location. Choose your district instead.');
    }
  }
}
