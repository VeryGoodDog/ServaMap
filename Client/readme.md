The client is used to generate the map shards that the server uses.

The process is similar to how the base game handles the map.
The main difference being a different shard renderer.

Once the chunk shards are done, they are sent to the server.

### Shard Generation Pipeline

Every time a chunk is marked dirty, it is added to a dictionary of recently
changed chunks and their modification time.

A game tick listener runs every second to check which chunks have changed more than 5 seconds ago.
This is done to rate limit chunk uploads.

If a chunk is old, it is rendered and sent to the server with several other chunks.
The client sends up to 16 chunks per render cycle in order to reduce potential lag spikes.

### Shard Rendering

Unlike the game's map, the renderer ignores seasonal coloration, and does not add noise.
This is done to make a uniform map.
The noise is not added to make features easier to see.
If defined, it will override the color of POI columns, eg paths.