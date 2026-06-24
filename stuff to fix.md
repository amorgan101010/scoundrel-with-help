# Claude's To-Do

## Stuff to fix

- sometimes cards go back into the deck face up. When this happens they can still be clicked. It seems related to all 4 cards not getting dealt.
- sometimes not all 4 cards deal out. Once when this happened when i fought an enemy and they went in the discard another card was drawn without changing rooms (said card was also still face up, as mentioned in the previous bullet)
- it is unclear what is happening when you click a second potion and nothing happens healing-wise (I know it is the rules you get one potion per room but there needs to be visual feedback those potions are void)
- I don't think you should be able to skip a room once a card has been used. IDK...maybe there should be a penalty to it or something, it can be handy. Put a pin in that.
- I reviewed the base rules further and when a room is skipped it gets put at the bottom of the deck, not shuffled in. Maybe potions and weapons go to the bottom and monsters get shuffled, since they can "wander" between rooms.

## Stuff to add

These are roughly in order but the extra rules will need to be kept in mind for all of them.

### Some debug values

like remaining cards in the deck and # in the discard, maybe even # of remaining cards of each suit. Maybe in a little popup window like Balatro, and same for showing what is in the discard. So I guess it is less debug values and more a feature, but it'll help me debug.

### Add unit and integration tests

This is #1 with a bullet, do this as soon as the stuff above has been fixed or maybe even as you go.

### Some sound FX!

I'll rustle up some samples, you just wire them in.

### Change the icon and name of the Executable

Just call it Scoundrel HD for now, make the icon a potion

### Show monsters "attached" to weapons

The monsters get attached to the weapon that slays them for a visual indicator of what it can attack, and when the weapon is discarded they all go with it. This matters more for the extra rules below and is impacted by them.

### Drag and Drop

This will matter more with the extra rules below but the game needs some draggin and droppin.

### Extra Rules

- The stuff in this youtube comment https://www.youtube.com/watch?v=7fP-QLtWQZs&lc=UgxnxBkhqD4AVqT_quF4AaABAg:

```txt
After looking at these comments I've started using a couple of the home rules they did (But I made some minor edits so they'd work together). 
- The diamond face cards as the blacksmiths (exactly how romanallgeier4661 did, so the Jack gets rid of one monster card off of an equipped weapon, Queen 2, King 3, and Ace all). Also, if a weapon has no durability loss, you can place a blacksmith under it to give it a bonus based on the value of the blacksmith, Jack is +1, Q +2, K+3, A+4. Being upgraded by an Ace also means that the weapon can attack an enemy that it one greater than its durability allows (this bonus is kinda situational but when it pays off it feels awesome and it means you can chain back up if you set it up right)
- The heart face cards as merchants as 4TheWizard said, with them allowing you to 'sell' weapons for health back, but with the amount of health back being the power of the weapon minus the amount of monster cards attached (with a lower cap of one health gained of course) plus a bonus based on the relative value of that face card (with jack having no bonus, queen adding 1 extra health, king adding 3 extra, and ace adding 5). 
- The party member idea of StartTheDayWithKeele could be used by the jokers, the red joker carrying potions that could be used later and the black joker carrying weapons so that you'd have more options in combat (tho he'd only be able to do combat that results in him not taking any damage or he dies and is discarded) or you could also (as if you have a d20 you probably have a full set of DnD dice) you could use a d8 (or some other value depending on what you think his health should be) and give him hp independently. 
Just wanted to put all of these ideas in one place as I thought they were really fun and meant I didn't have to search through all my cards each time looking for the red face cards lol. Also, for these rules, if you come across a diamond or hearts face card and are unable to or don't want to use them for any reason then you can recycle them into a random part of the deck (they don't have to go to the back because they can wander around the dungeon--this also means that if you run away from a room they won't go in the back, but will go somewhere random in the deck), but if you use one then it is gone forever.
```

I think of jokers like pockets that provide a bit of armor, one can hold a potion and one can hold a weapon.

All these new cards will need new Pillow art. The rules will need updating to reflect them.

It'll need to be broken down into smaller parts for me to test while you work.

Might be good to have a toggle between the classic ruleset and this one.

### Display health with dice

A D20 for player health, I guess a d8 to start for the jokers

### A Tutorial

For when help is not enough!

### Add some CI/CD with github actions

### Endless mode
