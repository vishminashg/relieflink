import 'package:flutter/material.dart';

const ink = Color(0xFF12263A);
const signal = Color(0xFF0F6F7C);
const critical = Color(0xFFB3261E);
const high = Color(0xFFB85C0A);
const medium = Color(0xFF2A6A8D);
const low = Color(0xFF4F6F4D);
const paper = Color(0xFFF3F6F7);

Color urgencyColor(String urgency) => switch (urgency) {
      'Critical' => critical,
      'High' => high,
      'Medium' => medium,
      _ => low,
    };

ThemeData buildTheme() {
  final scheme = ColorScheme.fromSeed(seedColor: signal, brightness: Brightness.light).copyWith(
    primary: signal,
    onPrimary: Colors.white,
    surface: Colors.white,
    onSurface: ink,
    error: critical,
  );
  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: paper,
    appBarTheme: const AppBarTheme(backgroundColor: ink, foregroundColor: Colors.white, centerTitle: false),
    floatingActionButtonTheme: const FloatingActionButtonThemeData(backgroundColor: signal, foregroundColor: Colors.white),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52), textStyle: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700)),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(52), textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
    ),
    inputDecorationTheme: const InputDecorationTheme(border: OutlineInputBorder(), filled: true, fillColor: Colors.white),
    textTheme: const TextTheme(
      headlineSmall: TextStyle(fontWeight: FontWeight.w800, color: ink),
      titleMedium: TextStyle(fontWeight: FontWeight.w700, color: ink),
    ),
  );
}
