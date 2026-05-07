# ArcFace Distributed App

A Windows Presentation Foundation (WPF) application for calculating and displaying the similarity rate between two user-selected face images.

This application uses the [ArcFace face recognition model](https://huggingface.co/garavv/arcface-onnx) together with a [custom-built NuGet package](https://www.nuget.org/packages/Kintobor.ArcFace.Locks.Embeddings/) to calculate distance and similarity rate between two face images. The application follows a distributed architecture and is separated into client and server components using the MVC design pattern.

- The client application provides the graphical user interface and performs face similarity calculations.
- The server application processes images and manages access to the SQLite database, which stores previously processed images.

## Client Application Features

- Select an image folder through a dialog window
- Display the absolute path of the selected folder
- Show connection status notifications when connected to the server
- Display a list of images stored in the database
- Display clickable image previews from the selected folder
- Compare two selected face images
- Calculate and display:
  - Facial distance
  - Similarity rate
- Visual progress bar for ongoing calculations
- Ability to cancel calculations at any time
- Error and notification dialog boxes for:
  - Missing image selections
  - Successful cancellation of calculations
- Delete all images from the database

## Examples of the Client Application UI

### Initial State
<img width="976" height="735" alt="image_2026-05-07_01-38-00" src="https://github.com/user-attachments/assets/4a49e8d8-0ba6-4e50-8569-a841c0571055" /><br>

### After Selecting an Image Folder
<img width="974" height="724" alt="image_2026-05-07_01-43-14" src="https://github.com/user-attachments/assets/cef894f3-3d9f-4c36-914a-a75b4c5d6e3e" /><br>

### Upon Successful Completion of Calculations
<img width="977" height="734" alt="image_2026-05-07_03-10-26" src="https://github.com/user-attachments/assets/4b96b367-981a-44ee-9738-806659b90e5a" /><br>

### Upon Cancellation of Calculations
<img width="978" height="734" alt="image_2026-05-07_03-11-32" src="https://github.com/user-attachments/assets/d1d4f162-8a89-4083-a8fd-418b1600ffcd" />

## Requirements
- Windows 10 or later
- .NET 8.0 SDK
- ArcFace ONNX model file

# To set up the application:
1. Clone this repository.
2. Download the ArcFace model from the [official ONNX GitHub page](https://github.com/onnx/models/blob/main/validated/vision/body_analysis/arcface/model/arcfaceresnet100-8.onnx).
3. Place the _arcfaceresnet100-8.onnx_ file in the root directory of the application.
4. Start the server application from the _Server_ directory with the following command prompt:
   ```
   dotnet run
   ```
5. Build the client application from the _Client_ directory with the following command prompt:
   ```
   dotnet build
   ```
6. Start the client application by navigating to _Client/bin/Debug/net8.0-windows_ directory and running _Client.exe_
