# GHPC Improved AI Mod

This mod tweaks the AI to engage enemies more sensibly in combat. It also provides a suite of commands you can issue to your platoon to better prepare for engagements.

Requires Melonloader 0.6.1 for GHPC.

To install, download the .DLL from the releases folder and place it into your /mods folder.

Additional configuration options are planned for later.

## AI Engagement Change

https://www.youtube.com/watch?v=N8X4wXYz_7w/


[![Alt](https://img.youtube.com/vi/N8X4wXYz_7w/0.jpg)](https://www.youtube.com/watch?v=N8X4wXYz_7w)

When AI units ingame see a target, most of them never turn to engage the enemy with their front armor, instead remaining on their original heading. This means flanking the enemy is often an easy win, since the AI never uses their front armor in such a situation.

Now, AI units (outside of your platoon's units) will face their front armor towards enemies and also stop to engage. This also dramatically increases their hit rate; my own testing showed it raises the AI's hitrate at maximum settings by over 100%. The AI should feel more dynamic and alive as a result of this. 

## Ping Command

https://www.youtube.com/watch?v=7BSzkH9DRZA



[![Alt](https://img.youtube.com/vi/7BSzkH9DRZA/0.jpg)](https://www.youtube.com/watch?v=7BSzkH9DRZA)

A ping system has been added. It is usable either in default TC view, or when using the binoculars. 

To ping something, align your aim point with any unit or spot on the terrain, and press your middle mouse button or the '5' key on your keyboard (not the numpad 5).

Pinging once will produce an *orange* marker; the vehicles in your platoon will watch the marker for 30 seconds or until a new ping is sent. Pinging a spot twice within a 10 meter distance and 2 seconds of the original ping will produce a *red* marker. The vehicles in your platoon will immediately halt and turn to face the red marker with their front armor. This is useful in engagements when your AI platoonmates have gotten stuck maneuvering and have their sides to the enemy, when they should be using their front armor.

The aim point is the center of the reticle in default commander view (when holding right click) or the center cross on the binoculars. The mod accounts for which binoculars you are using. Due to the wide variety of gunner reticles ingame, support for pinging while in gunner view is not planned, since a) the center of the aim point is not clear relative to onscreen markings, and b) this would conflict with features in Pact Increased Lethality, which also uses the middle mouse.

## IR Lights Toggle

https://www.youtube.com/watch?v=Nc2FzHoN3eo



[![Alt](https://img.youtube.com/vi/Nc2FzHoN3eo/0.jpg)](https://www.youtube.com/watch?v=Nc2FzHoN3eo)


A toggle for IR lights in your platoon has been added. You can access this toggle in the context menu or the tactical map by right clicking on your platoon marker. Very useful in night engagements when you don't want your platoon to give away your position by turning on IR lights. 

Note that selecting 'On' doesn't instantly force the AI to turn on IR lights, rather, it allows the AI to turn them on again, which they may choose to do so later rather than instantly.

## Hold Fire Toggle


https://www.youtube.com/watch?v=mGzt0JcOqS0

[![Alt](https://img.youtube.com/vi/mGzt0JcOqS0/0.jpg)](https://www.youtube.com/watch?v=mGzt0JcOqS0)

Self-explanatory. Stops the AI in your platoon from shooting any weapons. You can access this toggle in the context menu or the tactical map by right clicking on your platoon marker.  
