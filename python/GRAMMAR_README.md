# Grammar JSON File Readme

In order to setup speech recognition, you need to create a grammar JSON file that it will use for listening for responses from the user.

The grammar JSON file takes the following format: 

```
{
    "Rules": [
        {
            "Type": 0,
            "Key": "test rule 3",
            "Data": [
                {
                    "Type": 1,
                    "Key": null,
                    "Data": "Hey computer, I would like"
                },
                {
                    "Type": 4,
                    "Key": "food",
                    "Data": [
                        {
                            "Key": "Soup",
                            "Value": "soup"
                        },
                        {
                            "Key": "Fruit",
                            "Value": "fruit"
                        }
                    ]
                }
            ]
        },
    ],
    "Replacements": {
        "a pineapple": "fruit",
        "an orange": "fruit"
    },
    "Prefix": "Hey Computer"
}
```

The rules property is an array of different sets of grammar that is used to generate a list of phrases for PySpeechService to listen for. When it finds a matching phrase, it'll send a notification over gRPC with the identified phrase and rule.

Part of rules are key value pairs that are used to help identify specific options. For example, if you have a rule for starting an application, those key value pairs could be the list of possible applications. If any of those key value pairs is something that VOSK may have problems hearing, such as non-English fantasy names, you can use replacements to have similar sounding phrases. For example, VOSK might have issues hearing "GitHub" because it's not an English word. So, you could have a replacement of "get hub" as they key and "GitHub" as the value.

If all of your rules start with the same prefix, you can use the optional prefix property to have PySpeechService listen for that specifically at the start of each phrase before processing the rest. This can help prevent false positives.

## Rules 

Each rule is setup like the following:

```
"Type": 0,
"Key": "test rule 3",
"Data": [
    {
        "Type": 1,
        "Key": null,
        "Data": "Hey computer, I would like"
    },
    {
        "Type": 4,
        "Key": "food",
        "Data": [
            {
                "Key": "Soup",
                "Value": "soup"
            },
            {
                "Key": "Fruit",
                "Value": "fruit"
            }
        ]
    }
]
```

For each rule, the type needs to be 0. The key is the rule name that will be used and sent back via gRPC to match what is being done. For example, you might have a rule "start software" or "shutdown". This way you'll know what action to take no matter the exact phrase the user said. The data is a series of objects that are used to build up the phrases.

## Rule Speech Construction

As stated, you will need to provide rules with data to be able to build phrases. This data is a series of different components that it will combine to build these phrases to listen for. PySpeechService will combine all options to create the full list of phrases. For the above example, it would create two phrases: "Hey computer, I would like soup" and "Hey computer, I would like fruit". If you were to add two optional phrases in between the two of "tasty" or "delicious", it would create six phrases:

- Hey computer, I would like soup"
- Hey computer, I would like fruit"
- Hey computer, I would like tasty soup"
- Hey computer, I would like delicious soup"
- Hey computer, I would like tasty fruit"
- Hey computer, I would like delicious fruit"

With that in mind, the following will go over all of the different objects that can be added to a rule.

### Type 1: String

Type 1 is a required single string.

```
{
    "Type": 1,
    "Key": null,
    "Data": "Hey computer, "
},
```

### Type 2: One Of String Array

Type 2 is a string array where the user has to say one of the options. So with the below example, the user would have to say either "please give me" or "I would like".

```
{
    "Type": 2,
    "Key": null,
    "Data": [
        "please give me",
        "I would like"
    ]
}
```

### Type 3: Optional

Type 3 is an array of phrases where the user can include one of them, but is not required to. This is the equiavlent of type 2 but one of the items being an empty string. So below, the user can include either "tasty" or "delicious", but they don't have to.

```
{
    "Type": 3,
    "Key": null,
    "Data": [
        "tasty",
        "delicious"
    ]
}
```

### Type 4: Key Value Pairs

Type 4 is a series of different key value pairs that can be used to pass data back to you as to what option the user selected. This option the user stated will be passed back in the semantics map with the provided key being the key in the semantics map and the selected option being the value in the semantics map.

```
{
    "Type": 4,
    "Key": "food",
    "Data": [
        {
            "Key": "Soup",
            "Value": "soup"
        },
        {
            "Key": "Pizza",
            "Value": "pizza"
        },
        {
            "Key": "Bread",
            "Value": "bread"
        },
        {
            "Key": "Fruit",
            "Value": "fruit"
        }
    ]
}
```

So the above would create different phrases with the four different types of food. In the semantics object, it'll return { "food": "soup" } or whatever other option the user selected.

### Type 5: Grammar List

Type 5 is used in case you want have very different sets of phrases for a rule that may not be easy to combine into a single set of grammar details. With type 5, you can create entirely different sets of grammar that can be used for the same rule.

```
{
    "Type": 5,
    "Key": null,
    "Data": [
        {
            "Type": 0,
            "Key": null,
            "Data": [
                {
                    "Type": 1,
                    "Key": null,
                    "Data": "Hey computer, "
                },
                {
                    "Type": 2,
                    "Key": null,
                    "Data": [
                        "please give me",
                        "I would like"
                    ]
                },
                {
                    "Type": 4,
                    "Key": "food",
                    "Data": [
                        {
                            "Key": "Soup",
                            "Value": "soup"
                        },
                        {
                            "Key": "Fruit",
                            "Value": "fruit"
                        }
                    ]
                }
            ]
        },
        {
            "Type": 0,
            "Key": null,
            "Data": [
                {
                    "Type": 1,
                    "Key": null,
                    "Data": "Hey computer, "
                },
                {
                    "Type": 4,
                    "Key": "food",
                    "Data": [
                        {
                            "Key": "Soup",
                            "Value": "soup"
                        },
                        {
                            "Key": "Fruit",
                            "Value": "fruit"
                        }
                    ]
                }
                {
                    "Type": 2,
                    "Key": null,
                    "Data": [
                        "is my favorite food",
                        "is what I would like to eat today"
                    ]
                },
            ]
        }
    ]
}
```

With the above example, because they key value pairs of different types of food are in different locations, it's impossible to combine them into a single set of grammar details. However, with the above, it'll generate the following list of phrases:

- Hey computer, please give me soup
- Hey computer, please give me fruit
- Hey computer, I would like soup
- Hey computer, I would like ruit
- Hey computer, soup is my favorite food
- Hey computer, fruit is my favorite food
- Hey computer, soup is what I would like to eat today
- Hey computer, fruit is what I would like to eat today

With this, no matter what, it would still return the proper semantics with the appropriate food value the user selected.