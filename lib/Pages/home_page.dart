import 'package:connect/Pages/mouse_display.dart';
import 'package:flutter/material.dart';

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  final TextEditingController _controller = TextEditingController();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: EdgeInsetsGeometry.symmetric(horizontal: 12),
          child: Column(
            // mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SizedBox(height: 20),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    "Connect",
                    style: TextStyle(
                      fontSize: 23,
                      // color: Color.rgb(61, 218, 150),
                      color: Color.fromRGBO(61, 218, 150, 1),
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  IconButton(onPressed: () {}, icon: Icon(Icons.settings)),
                ],
              ),
              SizedBox(height: 10),
              Padding(
                padding: EdgeInsetsGeometry.symmetric(vertical: 5),
                child: Text(
                  "Connect to your laptop",
                  style: TextStyle(fontSize: 36, fontWeight: FontWeight.w800),
                ),
              ),
              Text(
                "Open MyApp on your laptop, then scan its code or type its address.",
                style: TextStyle(fontSize: 18),
              ),

              SizedBox(height: 50),
              Text("Laptop Address", style: TextStyle(fontSize: 18)),
              SizedBox(height: 20),
              TextField(
                cursorColor: Colors.blue,
                decoration: InputDecoration(
                  hint: Text("ws://localhost:8000/ws"),
                  hintStyle: TextStyle(color: Colors.grey),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  focusColor: Color.fromRGBO(61, 218, 150, 1),
                  focusedBorder: OutlineInputBorder(
                    borderSide: BorderSide(
                      color: Color.fromRGBO(61, 218, 150, 1),
                    ),
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
                controller: _controller,
              ),
              SizedBox(height: 50),
              Center(
                child: ClipRRect(
                  borderRadius: BorderRadiusGeometry.circular(12),
                  child: MaterialButton(
                    color: Color.fromRGBO(61, 218, 150, 1),
                    padding: EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    minWidth: double.infinity,
                    height: 50,
                    clipBehavior: Clip.antiAlias,
                    onPressed: () {
                      if (_controller.text.isNotEmpty) {
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (context) =>
                                MouseDisplay(uri: _controller.text.trim()),
                          ),
                        );
                        return;
                      }

                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(content: Text("Fill the address")),
                      );
                    },
                    child: Text("Connect"),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
