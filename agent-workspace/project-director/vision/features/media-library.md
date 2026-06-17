# Media Library & Multi-Modal Generation — Feature Spec

## What the User Accomplishes
The user browses, searches, and manages all media (images, audio, video) from all chats in a central library. AI can generate images and audio inline during conversations. All media renders inline in chat and is auto-saved to the library.

## Trigger
- Navigate to Media Library from Studio sidebar
- AI generates image (G4) or audio (G5) during conversation
- User uploads media via drag-and-drop (C9), camera (C22), or screenshot

## Detailed Behavior

### G1. Media Library Overview
- **Access:** Studio sidebar → "Media Library" nav item
- **Content:** All media across ALL chats:
  - AI-generated images (G4)
  - AI-generated audio (G5)
  - User-uploaded images, video, audio
  - Webcam captures (C22)
  - Screenshots (future T4)
- **Layout:** Grid of thumbnails

### G2. Media Library Filtering & Search
- **Filter by Type:** Images, Audio, Video, All (tabs or dropdown)
- **Filter by Chat:** Dropdown of chats that contain media
- **Filter by Date Range:** Date picker or presets
- **Search:** Text input searches by filename
- **Grid Display:** Images/video = thumbnails (square, ~150px). Audio = waveform icon with filename.

### G3. Media Actions
From Media Library, each item has:
- **View/Play:** Opens large preview (images) or player (audio/video)
- **Download:** Save copy to user-chosen location
- **Copy to Clipboard:** Copies image or file
- **Open in System App:** Launches default Windows app for file type
- **Delete from Library:** Confirmation dialog. Does NOT delete from chat where it originated.
- **Navigate to Source Chat:** Opens chat and scrolls to message containing the media

### G4. Image Generation
- When using a model supporting image generation (DALL-E, Stable Diffusion via API)
- AI can generate images directly in conversation
- Generated images render inline in chat at message width
- Auto-saved to Media Library
- **Caption:** Image caption or generation prompt shown below image

### G5. Audio Generation
- When using a model supporting audio/speech generation
- AI generates audio clips
- Appears as inline mini player in chat (play/pause, seek, download)
- Auto-saved to Media Library

### G6. Inline Media in Chat
- All media (generated or uploaded) renders inline in conversation
- Images: display at message width, click for full resolution
- Audio: mini player with play/pause and seek bar
- Video: embedded player with standard controls
- Each media item has: "Save to Disk" button, "View in Library" button

## Data
- [`data/media-item.md`](data/media-item.md) — media metadata, type, source chat, file path

## Success/Failure States
- **Empty Library:** "No media yet. Media appears here when AI generates images/audio or when you upload files in chats."
- **Generation Failure:** Inline error in chat: "Image generation failed. [error details]"
- **Unsupported Model:** If model doesn't support media generation, inline generation options are hidden/disabled.

## Permissions
- Single-user app.

## Interactions
- G1 sourced from C9 (uploads), C22 (camera), G4 (image gen), G5 (audio gen)
- G6 renders within C1/C2 (conversation view)
- Media deleted if source chat deleted and media not saved elsewhere (O5)
- Distinct from F (Artifacts): media = binary files (images, audio, video); artifacts = text-based files (code, documents)
