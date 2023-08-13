## Serv-a-Map, the map for your server

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/T6T53Q8SG)

Serv-a-Map is a mod that creates an online map you can view in your browser.

It is still a work in progress and is missing some core features. It does have:

- GeoJSON P/EoI export.
- Landmark manipulation via commands.
- Chunk tile generation.
- The ability to export all tiles in one large image.
- Tile resampling.
- Webserver hosting, the mod does not automatically host the online map.

It is currently missing:

- In-game map connection, landmarks, teleporters, and traders are not show in the in-game map.

### How it works

Serv-a-Map is *not* a server-side only mod. The client mod is used to create chunk shards.

The client then sends those shards to the server, which then processes them.
The shards are first checked against an internal database to make sure the server actually needs to
update anything.
If the server does need to update the tile, the database is updated and the shard is turned into a
PNG tile.

Also, the server scans new chunks for traders and teleporters.
If an entity of interest or point of interest is found it is compared against an internal database
and updated accordingly.

### How to build

You will need to change hint paths and `$VintageStoryInstall` in ServaMod.csproj

### Bug reporting

Please report bugs as issues on GitHub! Remember to include the game's logs.