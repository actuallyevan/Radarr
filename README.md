# Radarr

## Installation

This fork is designed to be a drop-in replacement for existing Radarr docker installations. Simply replace your Radarr docker image with `ghcr.io/actuallyevan/radarr:latest`

Sample docker compose:
```docker
radarr:
    image: ghcr.io/actuallyevan/radarr:latest
    container_name: radarr
    environment:
      - PUID=1000
      - PGID=1000
      - TZ=Etc/UTC
      # Add env vars for any tweaks you want to enable
      - IGNORE_MATCH_BY_ID_WARNING=true
      - IMPROVE_QUEUE_RESPONSIVENESS=true
      - DISABLE_MEDIA_COVER_CACHE=true
    volumes:
      - /path/to/radarr/data:/config
      # Additional volume mounts for your media, etc
    ports:
      - 7878:7878
    restart: unless-stopped
```

## Why this fork?

This fork aims to improve certain aspects of Radarr to make it work better with remote "infinite" library setups (Debrid/Usenet streaming, etc). This fork will be kept up-to-date with Radarr stable releases and you should be able to swap back and forth between them if needed.

## Fixes
These are universal bug-fixes that should be fixed in the original Radarr project. These might make it into a general release at some point.

- RefreshMonitoredDownloadsCommand: Debrid/Usenet mounting tools commonly issue this command after processing a download. But when this command is issued through the API or through the UI, it has a `Normal` priority, causing a buildup of queue items if lots of searches are triggered at once.
- Fix a bug where Radarr silently ignores torrents that have been previously imported, deleted and then grabbed again. Without this, repairs from Debrid/Usenet mounting tools don't work reliably.

## Tweaks
These are small changes to Radarr's behavior to optimize for Debrid/Usenet streaming setups. These can be enabled through environment variables. 

#### IGNORE_MATCH_BY_ID_WARNING

This setting turns off the warning:

```
Found matching movie via grab history, but release was matched to movie by ID. Manual Import required
``` 

This generally happens on indexers that include a tmdbId in releases that Radarr uses to match against movies while downloading. But during imports, if the files are obfuscated or the file/movie name doesn't match up with Radarr's expectations, it blocks automatic import.

⚠️ Only use this setting if you trust your indexers to provide the correct tmdbIds when they're present on releases.

#### IMPROVE_QUEUE_RESPONSIVENESS

Ensures that the `RefreshMonitoredDownloadsCommand` and `ProcessMonitoredDownloads` commands always execute immediately by reserving 3 additional slots for these commands. This improves UI responsiveness for the current state of the activity queue, ensuring the activity queue reflects what's happening in real time, even when many search tasks are queued.

#### DISABLE_MEDIA_COVER_CACHE

When set to `true`, Radarr will not download or store media cover images locally in the `MediaCover/` folder. Instead, cover URLs will point directly to remote sources (e.g., TMDB). This saves disk space and I/O for infinite/remote library setups where covers are only needed for display. Existing cached images are left untouched — delete the `MediaCover/` folder manually to reclaim space.

## Contributing

Feel free to open issues or pull requests for any changes you'd like to see.
