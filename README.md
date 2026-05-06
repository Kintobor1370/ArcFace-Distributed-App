# ArcFace Distributed App

  A Windows Presentation Foundation application that uses ArcFace model to display similarity rate between two face images imported by the user.
  All calculations are made on a server and displayed via a web client.
\\
# To set up the application:
1. Download this repository
2. Download the ArcFace model from the [official ONNX GitHub page](https://github.com/onnx/models/blob/main/validated/vision/body_analysis/arcface/model/arcfaceresnet100-8.onnx)
3. Place the _arcfaceresnet100-8.onnx_ file in the root of the application
4. Run the server by executing the following command prompt from the _Server_ folder:
   ```
   dotnet run
   ```
5. Build the client application by executing the following command prompt from the _Client_ folder:
   ```
   dotnet build
   ```
6. Navigate to _bin/Debug/net8.0-windows_ folder in the _Client_ folder and run _Client.exe_
