# **TP 1 \- XP Kick-off : C# - Dotnet - TDD et Logique Pure**

**Module :** M-4EADL-301 \- Développement Avancé & Extreme Programming

**Séance :** 1 / 8

**Focus :** Cycle TDD (Red/Green/Refactor), Structure de Projet Modulaire (.NET 10).

## **Objectif Principal**

Maîtriser le cycle **Test-Driven Development (TDD)** en appliquant les principes de l'Extreme Programming pour concevoir une classe de logique métier simple. Le projet sera structuré de manière modulaire pour être réutilisé et faire évoluer notre application Web, un outil back-end de suivi de progression et de gamification nommé **LevelUp**, au fil des 8 séances.

## **Technologies et Prérequis**

* **Runtime :** .NET 10
* **Langage :** C\# 14
* **Outils :** Visual Studio 2022 / JetBrains Rider, xUnit, Git
* **Nommage du Projet Fil Rouge :** `LevelUp`

## **Concept du Fil Rouge : LevelUp**

### **Le Défis : Gamifier l'Apprentissage**

**LevelUp** est un système back-end (API \+ BDD) dont le but est de suivre et de récompenser la progression d'utilisateurs (étudiants, développeurs, joueurs) en leur attribuant des points d'expérience (XP) et des badges basés sur les tâches accomplies:

* **Logique Cruciale et Sensible :** Le calcul de l'XP est une logique métier très sensible qui *doit* être parfaitement testée (parfait pour le TDD).  
* **Modélisation Évidente :** Il nécessite des entités claires (Utilisateurs, Activités, Badges, XP) pour la Séance 2\.  
* **API et Client :** Il est naturellement destiné à être une API pour que d'autres applications (comme notre client front ou MAUI) puissent soumettre des activités et consulter le profil utilisateur.

## **Partie A : Mise en place de l'Architecture et du TDD**

### **Étape 0 : Création de la Solution & Structure Modulaire**

L'application finale sera une API Web pour notre système de suivi de progression. Nous commençons par la couche métier et les tests pour respecter le TDD.

1. **Créer la Solution :**  
   * Créez une nouvelle solution vide nommée LevelUp.  
   * **Action Git :** Créez le dépôt Git et faites votre premier commit.  
2. **Créer le Projet de Logique Pure (Couche Métier) :**  
   * Ajoutez un nouveau projet de type **"Class Library"** (Bibliothèque de classes) à la solution.  
   * Nommez-le : `LevelUp.Core` (c'est ici que résidera toute la logique métier, indépendante d'ASP.NET ou de la BDD.)  
   * *Supprimez le fichier de classe par défaut (Class1.cs).*  
3. **Créer le Projet de Test :**  
   * Ajoutez un nouveau projet de type **"xUnit Test Project"** à la solution.  
   * Nommez-le : `LevelUp.Tests`
4. **Lier les Projets :**  
   * Ajoutez une référence du projet `LevelUp.Tests` vers `LevelUp.Core` (La couche de test doit pouvoir "voir" et tester la couche Core).  
   * **Action Git :** Commitez la structure (Architecture initiale : Core et Tests).

### **Étape 1 : TDD – XPCalculator**

Nous allons concevoir un calculateur de points d'expérience (XP) pour un utilisateur après une activité (résolution d'un bug, achèvement d'une tâche).

1. **RED (Le premier test échoue)**  
   * Dans le projet LevelUp.Tests, créez la classe XPCalculatorTests.  
   * Écrivez le premier test qui vérifie la règle de base : 
   **Une tâche simple terminée doit valoir 100 XP.**  
   ```c#
      // Exemple de signature de test attendue :
      [Fact]
      public void CalculateXP_ShouldReturnBaseXP_ForSimpleTask()
      {
         // Arrange
         var calculator = new XPCalculator();
         const int simpleTaskDurationMinutes = 30; // 30 minutes

         // Act
         int resultXP = calculator.CalculateXP(simpleTaskDurationMinutes);

         // Assert
         Assert.Equal(100, resultXP);
      }
     ```



   * Faites compiler le code en créant juste l'interface de la classe XPCalculator dans `LevelUp.Core`  
   * **Vérifiez que le test échoue (RED)**

2. **GREEN (Le minimum pour passer)**  
   * Dans la classe `XPCalculator` de `LevelUp.Core`, implémentez le strict minimum pour que le test `CalculateXP_ShouldReturnBaseXP_ForSimpleTask()` passe.  
   * **Vérifiez que le test passe (GREEN).**  

3. **REFACTOR (Nettoyage)**  
   * Examinez le code. Est-il propre ? Si oui pourquoi ? Vous passez à l'étape suivante. Si non, nettoyez sans casser les tests.
   * **Action Git :** Commitez le cycle réussi (TDD Cycle 1 : Base 100 XP).

### **Étape 2 : Itérations TDD (Règles supplémentaires pour le XP)**

Répétez le cycle **RED \-\> GREEN \-\> REFACTOR** pour les règles suivantes, une par une :

1. **Règle :** Si l'activité dure plus d'une heure (60 minutes), ajouter un bonus de 50 XP (ex: 75 minutes \= 150 XP).  
2. **Règle :** Si la durée fournie est négative ou nulle, renvoyer 0 XP.  
3. **Règle :** Créer une surcharge de la méthode `CalculateXP` qui prend un booléen `isCriticalBug`. Si c'est vrai, l'XP est multipliée par 2\.

### **Étape 3 : Synthèse et Livraison**

1. Assurez-vous que **tous les tests** passent.  
2. Vérifiez que la classe `XPCalculator` est aussi simple et lisible que possible (elle n'a pas besoin de base de données).  
3. **Action Git :** Faites le commit final de la séance sur une branche `TP1` 
4. TP1 terminé : `XPCalculator` OK.
