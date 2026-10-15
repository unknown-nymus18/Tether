import 'package:Tether/Pages/home_page.dart';
import 'package:Tether/Pages/settings_page.dart';
import 'package:Tether/providers/theme_provider.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

void main() {
  runApp(
    ChangeNotifierProvider(
      create: (_) => ThemeProvider(),
      child: const MyApp(),
    ),
  );
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<ThemeProvider>(
      builder: (context, value, child) => MaterialApp(
        theme: value.isDark
            ? ThemeData.dark(useMaterial3: true)
            : ThemeData.light(),
        debugShowCheckedModeBanner: false,
        home: SettingsPage(),
      ),
    );
  }
}
