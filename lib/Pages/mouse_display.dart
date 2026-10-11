import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:web_socket_channel/web_socket_channel.dart';

class MouseDisplay extends StatefulWidget {
  String uri;
  MouseDisplay({super.key, required this.uri});

  @override
  State<MouseDisplay> createState() => _MouseDisplayState();
}

class _MouseDisplayState extends State<MouseDisplay> {
  late final WebSocketChannel _channel;
  StreamSubscription<dynamic>? _subscription;
  bool _isConnected = false;
  bool _connectionFailed = false;

  double sensitivity = 1.0;

  static const Color _background = Color(0xFF15171E);
  static const Color _surface = Color(0xFF1D2029);
  static const Color _buttonSurface = Color(0xFF242833);
  static const Color _outline = Color(0xFF2D323E);
  // static const Color _accent = Color(0xFF3978FF);
  static const Color _mutedText = Color(0xFFAEB9CC);

  @override
  void initState() {
    super.initState();
    _channel = WebSocketChannel.connect(Uri.parse(widget.uri));
    unawaited(
      _channel.ready
          .then<void>((_) {
            _isConnected = true;
            _subscription = _channel.stream.listen(
              (message) => debugPrint('Server: $message'),
              onError: (Object error) {
                _isConnected = false;
                debugPrint('WebSocket error: $error');
              },
              onDone: () {
                _isConnected = false;
              },
            );
          })
          .catchError((Object error) {
            debugPrint('WebSocket connection failed: $error');
            if (mounted) {
              setState(() => _connectionFailed = true);
            }
          }),
    );
  }

  @override
  void dispose() {
    _isConnected = false;
    unawaited(_subscription?.cancel());
    _channel.sink.close();
    super.dispose();
  }

  void _sendCommand(Map<String, Object> command) {
    if (_isConnected) {
      _channel.sink.add(jsonEncode(command));
    }

    print(command);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: _background,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 14, 16, 16),
          child: Column(
            children: [
              if (_connectionFailed) ...[
                const Text(
                  'Connection failed. Could not connect to the server.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: Colors.redAccent, fontSize: 16),
                ),
                const SizedBox(height: 12),
              ],
              Expanded(
                child: Row(
                  children: [
                    Expanded(
                      child: GestureDetector(
                        behavior: HitTestBehavior.opaque,
                        onTap: () =>
                            _sendCommand({'type': 'click', 'button': 'left'}),
                        onPanUpdate: (details) => _sendCommand({
                          'type': 'move',
                          'dx': details.delta.dx * sensitivity,
                          'dy': details.delta.dy * sensitivity,
                        }),
                        child: Container(
                          alignment: Alignment.center,
                          decoration: BoxDecoration(
                            color: _surface,
                            borderRadius: BorderRadius.circular(13),
                            // border: Border.all(color: _accent, width: 1),
                          ),
                          child: const Text(
                            'Slide a finger to move the\ncursor\nTap to click',
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              color: _mutedText,
                              fontSize: 16,
                              height: 1.5,
                            ),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 10),
                    SizedBox(
                      width: 46,
                      child: GestureDetector(
                        behavior: HitTestBehavior.opaque,
                        onVerticalDragUpdate: (details) => _sendCommand({
                          'type': 'scroll',
                          'dy': details.delta.dy * sensitivity,
                        }),
                        onTap: () {
                          _sendCommand({'type': 'click', 'button': 'middle'});
                        },
                        child: Container(
                          alignment: Alignment.center,
                          decoration: BoxDecoration(
                            color: _surface,
                            borderRadius: BorderRadius.circular(15),
                            border: Border.all(color: _outline),
                          ),
                          child: const RotatedBox(
                            quarterTurns: 1,
                            child: Text(
                              'Scroll',
                              style: TextStyle(color: _mutedText, fontSize: 13),
                            ),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              SizedBox(
                height: 64,
                child: Row(
                  children: [
                    Expanded(
                      child: _ClickButton(
                        label: 'Left click',
                        onPressed: () =>
                            _sendCommand({'type': 'click', 'button': 'left'}),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: _ClickButton(
                        label: 'Right click',
                        onPressed: () =>
                            _sendCommand({'type': 'click', 'button': 'right'}),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ClickButton extends StatelessWidget {
  const _ClickButton({required this.label, required this.onPressed});

  final String label;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox.expand(
      child: FilledButton(
        onPressed: onPressed,
        style: FilledButton.styleFrom(
          backgroundColor: _MouseDisplayState._buttonSurface,
          foregroundColor: const Color(0xFFE2E7F0),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(13),
          ),
        ),
        child: Text(label, style: const TextStyle(fontSize: 15)),
      ),
    );
  }
}
