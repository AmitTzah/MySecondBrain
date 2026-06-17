# Media Library — Screen Specification

## Purpose

Browsable gallery of all media files across all chats — AI-generated images, user-uploaded images, audio files, video files, webcam captures, and screenshots. Filterable by type, source chat, and date range. Searchable by filename.

## Layout

Standard Studio frame with sidebar. Content area:
- Filter bar + search (top)
- Media grid (main area, thumbnails)

## Regions

### Region 1: Studio Sidebar
🖼️ Media nav item active.

### Region 2: Content Area

**Filter Bar:**
- Type filter: All | Images | Audio | Video | Screenshots | Webcam
- Source Chat dropdown
- Date Range: Today, This Week, This Month, All Time
- Sort: Newest (default), Oldest, Name A-Z, Size
- Search: "Search by filename..."

**Media Grid:**
- Responsive grid of thumbnail cards (4-6 columns depending on width)
- Each card: thumbnail/preview, filename (truncated), type badge, date, size
- Image thumbnails: actual image preview
- Audio: 🎵 placeholder with duration
- Video: 🎬 placeholder with duration
- Click card: opens full-size viewer/player overlay
- Viewer overlay: image full-size, audio/video player, metadata, "Open in System App", "Navigate to Source Chat", "Download", "Delete"
- Empty: "No media files yet. Upload images, generate AI images, or capture webcam photos during chats."

## Navigation

**Entry:** Studio sidebar → 🖼️ Media
**Exit:** Studio sidebar → any nav item

## Cross-References

- Feature spec: [`features/media-library.md`](../features/media-library.md) G1-G6
